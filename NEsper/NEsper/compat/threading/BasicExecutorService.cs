///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading;
using com.espertech.esper.compat.logging;


namespace com.espertech.esper.compat.threading
{
    /// <summary>
    /// Class that provides access to threadPool like services.  This class exists to
    /// provide an easier bridge between the CLR thread pool and the JVM thread pool
    /// mechanisms.
    /// </summary>

    public class BasicExecutorService : IExecutorService
    {
        private static int _idIncrement;

        private readonly int _id;
        private readonly ManualResetEvent _futuresMonitor;
        private readonly List<FutureBase> _futuresPending;
        private bool _isActive;
        private bool _isShutdown;
        private long _numExecuted;
        private long _numSubmitted;
        private long _numRecycled;

        /// <summary>
        /// Gets the number of items executed.
        /// </summary>
        /// <value>The num executed.</value>
        public int NumExecuted
        {
            get { return (int) Interlocked.Read(ref _numExecuted); }
        }

        public int NumSubmitted
        {
            get { return (int)Interlocked.Read(ref _numSubmitted); }
        }

        public BasicExecutorService()
        {
            _id = Interlocked.Increment(ref _idIncrement);

            int workerThreads;
            int completionThreads;

            _futuresMonitor = new ManualResetEvent(false);
            _futuresPending = new List<FutureBase>();
            _isActive = true;
            _isShutdown = false;
            _numExecuted = 0;
            _numSubmitted = 0;
            _numRecycled = 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BasicExecutorService"/> class.
        /// </summary>
        public BasicExecutorService(int maxNumThreads)
        {
            _id = Interlocked.Increment(ref _idIncrement);

            int workerThreads;
            int completionThreads;

            ThreadPool.GetMaxThreads(out workerThreads, out completionThreads);
            if (maxNumThreads != -1)
            {
                ThreadPool.SetMaxThreads(maxNumThreads, completionThreads);
            }

            if (Log.IsDebugEnabled)
            {
                Log.Debug(String.Format(".ctor - Creating Executor with maxNumThreads = {0}", maxNumThreads));
            }

            _futuresMonitor = new ManualResetEvent(false);
            _futuresPending = new List<FutureBase>();
            _isActive = true;
            _isShutdown = false;
            _numExecuted = 0;
        }

        /// <summary>
        /// Dispatches the future.
        /// </summary>
        private void DispatchFuture(Object userData)
        {
            var future = userData as FutureBase;

            try
            {
                if (_isActive)
                {
                    if (Log.IsInfoEnabled)
                    {
                        Log.Info(".DispatchFuture - Instance {0} dispatching item", _id);
                    }

                    if (future != null)
                    {
                        future.Invoke();
                        Interlocked.Increment(ref _numExecuted);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(".DispatchFuture - Instance {0} failed", _id);
                Log.Error(".DispatchFuture", e);
            }
            finally
            {
                Recycle(future);
            }
        }

        private void Recycle(FutureBase future)
        {
            lock (_futuresPending)
            {
                _futuresPending.Remove(future);

                if (Log.IsInfoEnabled) {
                    Log.Info(
                        ".DispatchFuture - Instance {0} done dispatching: {1} pending",
                        _id,
                        _futuresPending.Count);
                }

                if (_futuresPending.Count == 0)
                {
                    _futuresMonitor.Set();
                }
            }

            Interlocked.Increment(ref _numRecycled);
        }

        /// <summary>
        /// Submits the specified runnable to the thread pool.
        /// </summary>
        /// <param name="runnable">The runnable.</param>
        public Future<Object> Submit(Action runnable)
        {
            Func<Object> function =
                () => { runnable.Invoke(); return null; };
            return Submit(function);
        }

        /// <summary>
        /// Submits the specified callable to the thread pool.
        /// </summary>
        /// <param name="callable">The callable.</param>
        /// <returns></returns>
        public Future<T> Submit<T>(ICallable<T> callable)
        {
            Func<T> function = callable.Call;
            return Submit(function);
        }

        /// <summary>
        /// Submits the specified callable to the thread pool.
        /// </summary>
        /// <param name="callable">The callable.</param>
        /// <returns></returns>
        public Future<T> Submit<T>(Func<T> callable)
        {
            if (_isShutdown)
            {
                throw new IllegalStateException("ExecutorService is shutdown");
            }

            Interlocked.Increment(ref _numSubmitted);

            var future = new FutureImpl<T> {Callable = callable};

            lock (_futuresPending)
            {
                _futuresPending.Add(future);

                if (Log.IsInfoEnabled) {
                    Log.Info(
                        ".Submit - Instance {0} queued user work item: {1} pending",
                        _id,
                        _futuresPending.Count);
                }
            }
 
            ThreadPool.QueueUserWorkItem(DispatchFuture, future);

            return future;
        }

        /// <summary>
        /// Shutdowns this instance.
        /// </summary>
        public void Shutdown()
        {
            if (Log.IsInfoEnabled)
            {
                Log.Info(".Shutdown - Marking instance {0} to avoid further queuing", _id);
            }

            // Mark the executor as inactive so that we
            // don't take any new callables.
            _isShutdown = true;
        }

        /// <summary>
        /// Awaits the termination.
        /// </summary>
        public void AwaitTermination()
        {
            AwaitTermination(new TimeSpan(0, 0, 15));
        }

        /// <summary>
        /// Awaits the termination.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        public void AwaitTermination(TimeSpan timeout)
        {
            if (Log.IsInfoEnabled)
            {
                Log.Info(
                    ".AwaitTermination - Instance {0} waiting for {1} tasks to complete",
                    _id, _futuresPending.Count);
            }

            if (_futuresPending.Count != 0)
            {
                _futuresMonitor.WaitOne(timeout, true);
                if ( _futuresPending.Count != 0 )
                {
                    _futuresPending.ForEach(
                        delegate(FutureBase futureBase)
                        {
                            Log.Warn(".AwaitTermination - Forceably terminating future");
                            futureBase.Kill();
                        });
                }
            }

            if (Log.IsInfoEnabled) {
                Log.Info(
                    ".AwaitTermination - Instance {0} marked inactive with {1} tasks to complete",
                    _id,
                    _futuresPending.Count);
            }

            _isActive = false;
        }

        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }

    /// <summary>
    /// Class that provides access to threadPool like services.  This class exists to
    /// provide an easier bridge between the CLR thread pool and the JVM thread pool
    /// mechanisms.
    /// </summary>
    /// 
    public class Executors
    {
        /// <summary>
        /// Supposably creates a new thread pool and returns the executor.  Ours does
        /// nothing as we use the CLR thread pool.
        /// </summary>
        /// <returns></returns>
        public static BasicExecutorService NewCachedThreadPool()
        {
            return new BasicExecutorService(-1);
        }

        /// <summary>
        /// Supposably creates a new thread pool and returns the executor.  Ours does
        /// nothing as we use the CLR thread pool.
        /// </summary>
        /// <param name="maxNumThreads">The max num threads.</param>
        /// <returns></returns>
        public static BasicExecutorService NewFixedThreadPool(int maxNumThreads)
        {
            return new BasicExecutorService(maxNumThreads);
        }

        /// <summary>
        /// Supposably creates a new thread pool and returns the executor.
        /// </summary>
        /// <returns></returns>
        public static BasicExecutorService NewSingleThreadExecutor()
        {
            return new BasicExecutorService(1);
        }
    }

    public interface Future<T>
    {
        /// <summary>
        /// Gets a value indicating whether this instance has value.
        /// </summary>
        /// <value><c>true</c> if this instance has value; otherwise, <c>false</c>.</value>
        bool HasValue { get; }

        /// <summary>
        /// Gets the value. If a value is not available before the timeout expires,
        /// a TimeoutException will be thrown.
        /// </summary>
        /// <param name="timeOut">The time out.</param>
        /// <returns></returns>
        T GetValue(TimeSpan timeOut);

        /// <summary>
        /// Gets the result value from the execution.
        /// </summary>
        /// <returns></returns>
        T GetValueOrDefault();
    }

    /// <summary>
    /// Default implementation of a future
    /// </summary>
    public interface Future : Future<object>
    {
    }

    /// <summary>
    /// Base class for all future implementations
    /// </summary>
    abstract internal class FutureBase
    {
        private Thread _execThread;

        /// <summary>
        /// Gets the exec thread.
        /// </summary>
        /// <value>The exec thread.</value>
        public Thread ExecThread
        {
            get { return _execThread; }
        }

        /// <summary>
        /// Invokes the impl.
        /// </summary>
        protected abstract void InvokeImpl();

        /// <summary>
        /// Invokes this instance.
        /// </summary>
        public void Invoke()
        {
            Interlocked.Exchange(ref _execThread, Thread.CurrentThread);
            try
            {
                InvokeImpl();
            }
            catch (ThreadInterruptedException)
            {
                Log.Warn(".Invoke - Thread Interrupted");
            }
            finally
            {
                Interlocked.Exchange(ref _execThread, null);
            }
        }

        /// <summary>
        /// Kills this instance.
        /// </summary>
        internal void Kill()
        {
            Thread tempThread = Interlocked.Exchange(ref _execThread, null);
            if (tempThread != null)
            {
                Log.Warn(".Kill - Forceably terminating future");
                tempThread.Interrupt();
            }
        }

        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }

    internal class FutureImpl<T> 
        : FutureBase
        , Future<T>
    {
        private T _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="FutureImpl&lt;T&gt;"/> class.
        /// </summary>
        public FutureImpl()
        {
            _hasValue = false;
            _value = default(T);
        }

        private bool _hasValue;

        /// <summary>
        /// Gets a value indicating whether this instance has value.
        /// </summary>
        /// <value><c>true</c> if this instance has value; otherwise, <c>false</c>.</value>
        public bool HasValue
        {
            get { return _hasValue; }
        }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        public T Value
        {
            get
            {
                if (! HasValue) {
                    throw new InvalidOperationException();
                }

                return _value;
            }
            set
            {
                _value = value;
                _hasValue = true;
            }
        }

        /// <summary>
        /// Gets the value. If a value is not available before the timeout expires,
        /// a TimeoutException will be thrown.
        /// </summary>
        /// <param name="timeOut">The time out.</param>
        /// <returns></returns>
        public T GetValue(TimeSpan timeOut)
        {
            throw new NotSupportedException("implementation does not provide blocking mechanics");
        }

        /// <summary>
        /// Gets the result value from the execution.
        /// </summary>
        /// <returns></returns>
        public T GetValueOrDefault()
        {
            if (! _hasValue) {
                return default(T);
            }

            return Value;
        }

        /// <summary>
        /// Gets or sets the callable.
        /// </summary>
        /// <value>The callable.</value>
        internal Func<T> Callable { get; set; }

        /// <summary>
        /// Invokes this instance.
        /// </summary>
        protected override void InvokeImpl()
        {
            Value = Callable.Invoke();
            if ( Log.IsInfoEnabled )
            {
                Log.Info("Invoke - Completed with return value of {0}", Value);
            }
        }

        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }
}
