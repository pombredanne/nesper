///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.client;
using com.espertech.esper.compat;
using com.espertech.esper.metrics.instrumentation;
using com.espertech.esper.supportregression.bean;
using com.espertech.esper.supportregression.client;

using NUnit.Framework;

namespace com.espertech.esper.regression.client
{
    [TestFixture]
    public class TestSubscriberInvalid 
    {
        private EPServiceProvider _epService;
        private EPAdministrator _epAdmin;
    
        [SetUp]
        public void SetUp()
        {
            Configuration config = SupportConfigFactory.GetConfiguration();
            String pkg = typeof(SupportBean).Namespace;
            config.AddEventTypeAutoName(pkg);
            _epService = EPServiceProviderManager.GetDefaultProvider(config);
            _epService.Initialize();
            if (InstrumentationHelper.ENABLED) { InstrumentationHelper.StartTest(_epService, GetType(), GetType().FullName); }
            _epAdmin = _epService.EPAdministrator;
        }
    
        [TearDown]
        public void TearDown()
        {
            if (InstrumentationHelper.ENABLED) { InstrumentationHelper.EndTest(); }
            _epAdmin = null;
        }
    
        [Test]
        public void TestBindWildcardJoin()
        {
            EPStatement stmtOne = _epAdmin.CreateEPL("select * from SupportBean");
            TryInvalid(this, stmtOne, "Subscriber object does not provide a public method by name 'Update'");
            TryInvalid(new DummySubscriberEmptyUpd(), stmtOne, "No suitable subscriber method named 'Update' found, expecting a method that takes 1 parameter of type " + Name.Of<SupportBean>() + "");
            TryInvalid(new DummySubscriberMultipleUpdate(), stmtOne, "No suitable subscriber method named 'Update' found, expecting a method that takes 1 parameter of type " + Name.Of<SupportBean>() + "");
            TryInvalid(new DummySubscriberUpdate(), stmtOne, "Subscriber method named 'Update' for parameter number 1 is not assignable, expecting type '" + Name.Of<SupportBean>() + "' but found type '" + Name.Of<SupportMarketDataBean>() + "'");
            TryInvalid(new DummySubscriberPrivateUpd(), stmtOne, "Subscriber object does not provide a public method by name 'Update'");

            EPStatement stmtTwo = _epAdmin.CreateEPL("select intPrimitive from SupportBean");
            String message = "Subscriber 'updateRStream' method footprint must match 'update' method footprint";
            TryInvalid(new DummySubscriberMismatchUpdateRStreamOne(), stmtTwo, message);
            TryInvalid(new DummySubscriberMismatchUpdateRStreamTwo(), stmtTwo, message);
        }
    
        [Test]
        public void TestInvocationTargetEx()
        {
            // smoke test, need to consider log file; test for ESPER-331 
            EPStatement stmt = _epAdmin.CreateEPL("select * from SupportMarketDataBean");
            stmt.Subscriber = new DummySubscriberException();
            stmt.Events += (sender, args) => { throw new ApplicationException("test exception 1"); };
            stmt.Events += (sender, args) => { throw new ApplicationException("test exception 2"); };
            stmt.AddEventHandlerWithReplay(
                (sender, args) => { throw new ApplicationException("test exception 3"); });
    
            // no exception expected
            _epService.EPRuntime.SendEvent(new SupportMarketDataBean("IBM", 0, 0L, ""));
        }
    
        private void TryInvalid(Object subscriber, EPStatement stmt, String message)
        {
            try
            {
                stmt.Subscriber = subscriber;
                Assert.Fail();
            }
            catch (EPSubscriberException ex)
            {
                Assert.AreEqual(message, ex.Message);
            }
        }
    
        public class DummySubscriberException
        {
            public void Update(SupportMarketDataBean bean)
            {
                throw new ApplicationException("DummySubscriberException-generated");
            }
        }
    
        public class DummySubscriberEmptyUpd
        {
            public void Update() {}
        }
    
        public class DummySubscriberPrivateUpd
        {
            private void Update(SupportBean bean) {}
        }
    
        public class DummySubscriberUpdate
        {
            public void Update(SupportMarketDataBean dummy) {}
        }
    
        public class DummySubscriberMultipleUpdate
        {
            public void Update(long x) {}
            public void Update(int x) {}
        }

        public class DummySubscriberMismatchUpdateRStreamOne
        {
            public void Update(int value) { }
            public void UpdateRStream(EPStatement stmt, int value) { }
        }

        public class DummySubscriberMismatchUpdateRStreamTwo
        {
            public void Update(EPStatement stmt, int value) { }
            public void UpdateRStream(int value) { }
        }
    }
}
