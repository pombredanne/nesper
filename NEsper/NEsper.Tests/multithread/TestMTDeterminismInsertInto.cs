///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////


using System;
using System.Collections;
using System.Threading;

using com.espertech.esper.client;
using com.espertech.esper.client.scopetest;
using com.espertech.esper.compat.logging;
using com.espertech.esper.compat.threading;
using com.espertech.esper.support.bean;
using com.espertech.esper.support.client;

using NUnit.Framework;

namespace com.espertech.esper.multithread
{
    /// <summary>
    /// Test for multithread-safety and deterministic behavior when using insert-into.
    /// </summary>
    [TestFixture]
    public class TestMTDeterminismInsertInto 
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [TearDown]
        public void TearDown()
        {
            EPServiceProviderManager.PurgeAllProviders();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [Test]
        public void TestSceneOneSuspend()
        {
            TrySendCountFollowedBy(4, 10000, ConfigurationEngineDefaults.Threading.Locking.SUSPEND);
        }
    
        [Test]
        public void TestSceneOneSpin()
        {
            TrySendCountFollowedBy(4, 10000, ConfigurationEngineDefaults.Threading.Locking.SPIN);
        }

        [Test]
        public void TestSceneTwo()
        {
            TryChainedCountSum(3, 10000);
        }
    
        [Test]
        public void TestSceneThree()
        {
            TryMultiInsertGroup(3, 10, 1000);
        }
    
        private void TryMultiInsertGroup(int numThreads, int numStatements, int numEvents)
        {
            Configuration config = SupportConfigFactory.GetConfiguration();
            // This should fail all test in this class
            // config.EngineDefaults.ThreadingConfig.InsertIntoDispatchPreserveOrder = false;
    
            EPServiceProvider engine = EPServiceProviderManager.GetDefaultProvider(config);
            engine.Initialize();
    
            // setup statements
            EPStatement[] insertIntoStmts = new EPStatement[numStatements];
            for (int i = 0; i < numStatements; i++)
            {
                insertIntoStmts[i] = engine.EPAdministrator.CreateEPL("insert into MyStream select " + i + " as ident,count(*) as cnt from " + typeof(SupportBean).FullName);
            }
            EPStatement stmtInsertTwo = engine.EPAdministrator.CreateEPL("select ident, sum(cnt) as mysum from MyStream group by ident");
            SupportUpdateListener listener = new SupportUpdateListener();
            stmtInsertTwo.Events += listener.Update;
    
            // execute
            var threadPool = Executors.NewFixedThreadPool(numThreads);
            var future = new Future<bool>[numThreads];
            var sharedStartLock = ReaderWriterLockManager.CreateLock(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            using (sharedStartLock.AcquireWriteLock()) {
                for (int i = 0; i < numThreads; i++) {
                    future[i] = threadPool.Submit(
                        new SendEventRWLockCallable(i, sharedStartLock.ReadLock, engine, EnumerationGenerator.Create(numEvents)));
                }
                Thread.Sleep(100);
            }
    
            threadPool.Shutdown();
            threadPool.AwaitTermination(new TimeSpan(0,0,10));
    
            for (int i = 0; i < numThreads; i++)
            {
                Assert.IsTrue((bool) future[i].GetValueOrDefault());
            }
    
            // assert result
            EventBean[] newEvents = listener.GetNewDataListFlattened();
            ArrayList[] resultsPerIdent = new ArrayList[numStatements];
            foreach (EventBean theEvent in newEvents)
            {
                int ident = (int)theEvent.Get("ident");
                if (resultsPerIdent[ident] == null)
                {
                    resultsPerIdent[ident] = new ArrayList();
                }
                long mysum = (long) theEvent.Get("mysum");
                resultsPerIdent[ident].Add(mysum);
            }
    
            for (int statement = 0; statement < numStatements; statement++)
            {
                for (int i = 0; i < numEvents - 1; i++)
                {
                    long expected = Total(i + 1);
                    Assert.AreEqual(expected, resultsPerIdent[statement][i]);
                }
            }
    
            // destroy
            for (int i = 0; i < numStatements; i++)
            {
                insertIntoStmts[i].Dispose();
            }
            stmtInsertTwo.Dispose();
        }
    
        private void TryChainedCountSum(int numThreads, int numEvents)
        {
            Configuration config = SupportConfigFactory.GetConfiguration();
            config.EngineDefaults.ThreadingConfig.IsInternalTimerEnabled = false;
            // This should fail all test in this class
            // config.EngineDefaults.ThreadingConfig.InsertIntoDispatchPreserveOrder = false;
    
            EPServiceProvider engine = EPServiceProviderManager.GetDefaultProvider(config);
            engine.Initialize();
    
            // setup statements
            EPStatement stmtInsertOne = engine.EPAdministrator.CreateEPL("insert into MyStreamOne select count(*) as cnt from " + typeof(SupportBean).FullName);
            EPStatement stmtInsertTwo = engine.EPAdministrator.CreateEPL("insert into MyStreamTwo select sum(cnt) as mysum from MyStreamOne");
            EPStatement stmtInsertThree = engine.EPAdministrator.CreateEPL("select * from MyStreamTwo");
            SupportUpdateListener listener = new SupportUpdateListener();
            stmtInsertThree.Events += listener.Update;
#if false
            stmtInsertThree.Events += (sender, eargs) =>
            {
                Console.WriteLine("Event: {0}", eargs.NewEvents[0].Get("mysum"));
            };
#endif

            // execute
            var threadPool = Executors.NewFixedThreadPool(numThreads);
            var future = new Future<bool>[numThreads];
            var sharedStartLock = ReaderWriterLockManager.CreateLock(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            using (sharedStartLock.AcquireWriteLock()) {
                for (int i = 0; i < numThreads; i++) {
                    future[i] = threadPool.Submit(
                        new SendEventRWLockCallable(i, sharedStartLock.ReadLock, engine, EnumerationGenerator.Create(numEvents)));
                }
                Thread.Sleep(100);
            }
    
            threadPool.Shutdown();
            threadPool.AwaitTermination(new TimeSpan(0, 0, 10));
    
            for (int i = 0; i < numThreads; i++)
            {
                Assert.IsTrue((bool) future[i].GetValueOrDefault());
            }

            // assert result
            EventBean[] newEvents = listener.GetNewDataListFlattened();
            for (int i = 0; i < numEvents - 1; i++)
            {
                long expected = Total(i + 1);
                Assert.AreEqual(expected, newEvents[i].Get("mysum"));
            }
    
            stmtInsertOne.Dispose();
            stmtInsertTwo.Dispose();
            stmtInsertThree.Dispose();
        }
    
        private long Total(int num)
        {
            long total = 0;
            for (int i = 1; i < num + 1; i++)
            {
                total += i; 
            }
            return total;
        }
    
        private void TrySendCountFollowedBy(int numThreads, int numEvents, ConfigurationEngineDefaults.Threading.Locking locking)
        {
            Configuration config = SupportConfigFactory.GetConfiguration();
            config.EngineDefaults.ThreadingConfig.InsertIntoDispatchLocking = locking;
            config.EngineDefaults.ThreadingConfig.InsertIntoDispatchTimeout = 5000; // 5 second timeout
            // This should fail all test in this class
            // config.EngineDefaults.ThreadingConfig.InsertIntoDispatchPreserveOrder = false;
    
            EPServiceProvider engine = EPServiceProviderManager.GetDefaultProvider(config);
            engine.Initialize();
    
            // setup statements
            EPStatement stmtInsert = engine.EPAdministrator.CreateEPL("insert into MyStream select count(*) as cnt from " + typeof(SupportBean).FullName);
            stmtInsert.Events += ((sender, e) => Log.Debug(".Update cnt=" + e.NewEvents[0].Get("cnt")));
    
            SupportUpdateListener[] listeners = new SupportUpdateListener[numEvents];
            for (int i = 0; i < numEvents; i++)
            {
                String text = "select * from pattern [MyStream(cnt=" + (i + 1) + ") -> MyStream(cnt=" + (i + 2) + ")]";
                EPStatement stmt = engine.EPAdministrator.CreateEPL(text);
                listeners[i] = new SupportUpdateListener();
                stmt.Events += listeners[i].Update;
            }
    
            // execute
            var threadPool = Executors.NewFixedThreadPool(numThreads);
            var future = new Future<bool>[numThreads];
            var sharedStartLock = ReaderWriterLockManager.CreateLock(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            using (sharedStartLock.AcquireWriteLock()) {
                for (int i = 0; i < numThreads; i++) {
                    future[i] = threadPool.Submit(new SendEventRWLockCallable(
                            i, sharedStartLock.ReadLock, engine,
                            EnumerationGenerator.Create(numEvents)));
                }
                Thread.Sleep(100);
            }
    
            threadPool.Shutdown();
            threadPool.AwaitTermination(new TimeSpan(0, 0, 10));
    
            for (int i = 0; i < numThreads; i++)
            {
                Assert.IsTrue((bool) future[i].GetValueOrDefault());
            }
    
            // assert result
            for (int i = 0; i < numEvents - 1; i++)
            {
                Assert.AreEqual(1,listeners[i].NewDataList.Count,"Listener not invoked: #" + i);
            }
        }
    }
}
