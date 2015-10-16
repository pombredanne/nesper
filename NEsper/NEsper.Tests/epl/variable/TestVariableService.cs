///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.compat;
using com.espertech.esper.compat.threading;
using com.espertech.esper.schedule;
using com.espertech.esper.support.events;
using com.espertech.esper.timer;
using NUnit.Framework;

namespace com.espertech.esper.epl.variable
{
    [TestFixture]
    public class TestVariableService
    {
        #region Setup/Teardown

        [SetUp]
        public void SetUp()
        {
            _service = new VariableServiceImpl(10000, new SchedulingServiceImpl(new TimeSourceServiceImpl()), SupportEventAdapterService.Service, null);
        }

        #endregion

        private VariableService _service;

        // Start Count threads
        // each thread performs X loops
        // each loop gets a unique number Y from a shared object and performs setVersion in the synchronized block
        // then the thread performs reads, write and read of shared variables, writing the number Y
        // ==> the reads should not see any higher number (unless watemarks reached)
        // ==> reads should produce the exact same result unless setVersion called
        private void TryMT(int numThreads, int numLoops, int numVariables)
        {
            var coord = new VariableVersionCoord(_service);

            int ord = 'A';

            // create variables
            var variables = new String[numVariables];
            for (int i = 0; i < numVariables; i++) {
                variables[i] = String.Format("{0}", ((char) (ord + i)));
                _service.CreateNewVariable(null, variables[i], typeof(int).FullName, false, false, false, 0, null);
                _service.AllocateVariableState(variables[i], 0, null);
            }

            BasicExecutorService threadPool = Executors.NewFixedThreadPool(numThreads);
            var callables = new VariableServiceCallable[numThreads];
            var future = new Future<bool>[numThreads];
            for (int i = 0; i < numThreads; i++) {
                callables[i] = new VariableServiceCallable(variables, _service, coord, numLoops);
                future[i] = threadPool.Submit(callables[i]);
            }

            threadPool.Shutdown();
            threadPool.AwaitTermination(new TimeSpan(0, 0, 10));

            for (int i = 0; i < numThreads; i++) {
                Assert.IsTrue(future[i].GetValueOrDefault());
            }

            //Console.Out.WriteLine(service.ToString());
            // Verify results per thread
            for (int i = 0; i < callables.Length; i++) {
                int[][] result = callables[i].Results;
                int[] marks = callables[i].Marks;
            }
        }

        private void ReadCompare(String[] variables, Object value)
        {
            _service.SetLocalVersion();
            for (int i = 0; i < variables.Length; i++) {
                Assert.AreEqual(value, _service.GetReader(variables[i], 0).Value);
            }
        }

        [Test]
        public void TestInvalid()
        {
            _service.CreateNewVariable<long?>(null, "a", false, null, null);
            _service.AllocateVariableState("a", 0, null);
            Assert.IsNull(_service.GetReader("dummy", 0));

            try {
                _service.CreateNewVariable<long?>(null, "a", false, null, null);
                Assert.Fail();
            }
            catch (VariableExistsException e) {
                Assert.AreEqual("Variable by name 'a' has already been created", e.Message);
            }
        }

        [Test]
        public void TestMultithreadedOne()
        {
            TryMT(2, 10000, 4);
        }

        [Test]
        public void TestMultithreadedZero()
        {
            TryMT(4, 5000, 8);
        }

        [Test]
        public void TestPerfSetVersion()
        {
            long start = PerformanceObserver.MilliTime;
            for (int i = 0; i < 100000; i++) {
                _service.SetLocalVersion();
            }
            long end = PerformanceObserver.MilliTime;
            long delta = (end - start);
            Assert.IsTrue(delta < 100, "delta=" + delta);
        }

        [Test]
        public void TestReadWrite()
        {
            Assert.IsNull(_service.GetReader("a", 0));

            _service.CreateNewVariable<long>(null, "a", false, 100L, null);
            _service.AllocateVariableState("a", 0, null);
            VariableReader reader = _service.GetReader("a", 0);
            Assert.AreEqual(typeof(long?), reader.VariableMetaData.VariableType);
            Assert.AreEqual(100L, reader.Value);

            _service.Write(reader.VariableMetaData.VariableNumber, 0, 101L);
            _service.Commit();
            Assert.AreEqual(100L, reader.Value);
            _service.SetLocalVersion();
            Assert.AreEqual(101L, reader.Value);

            _service.Write(reader.VariableMetaData.VariableNumber, 0, 102L);
            _service.Commit();
            Assert.AreEqual(101L, reader.Value);
            _service.SetLocalVersion();
            Assert.AreEqual(102L, reader.Value);
        }

        [Test]
        public void TestRollover()
        {
            _service = new VariableServiceImpl(
                VariableServiceImpl.ROLLOVER_READER_BOUNDARY - 100, 
                10000,
                new SchedulingServiceImpl(new TimeSourceServiceImpl()), 
                SupportEventAdapterService.Service,
                null);

            String[] variables = "a,b,c,d".Split(',');

            var readers = new VariableReader[variables.Length];
            for (int i = 0; i < variables.Length; i++) {
                _service.CreateNewVariable<long>(null, variables[i], false, 100L, null);
                _service.AllocateVariableState(variables[i], 0, null);
                readers[i] = _service.GetReader(variables[i], 0);
            }

            for (int i = 0; i < 1000; i++) {
                for (int j = 0; j < variables.Length; j++) {
                    _service.Write(readers[j].VariableMetaData.VariableNumber, 0, 100L + i);
                    _service.Commit();
                }
                ReadCompare(variables, 100L + i);
            }
        }
    }
}
