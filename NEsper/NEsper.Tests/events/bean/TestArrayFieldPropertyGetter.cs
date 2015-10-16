///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Reflection;

using com.espertech.esper.client;
using com.espertech.esper.support.bean;
using com.espertech.esper.support.events;

using NUnit.Framework;

namespace com.espertech.esper.events.bean
{
    [TestFixture]
    public class TestArrayFieldPropertyGetter 
    {
        private ArrayFieldPropertyGetter _getter;
        private ArrayFieldPropertyGetter _getterOutOfBounds;
        private EventBean _event;
        private SupportLegacyBean _bean;
    
        [SetUp]
        public void SetUp()
        {
            _bean = new SupportLegacyBean(new[] {"a", "b"});
            _event = SupportEventBeanFactory.CreateObject(_bean);
    
            _getter = MakeGetter(0);
            _getterOutOfBounds = MakeGetter(int.MaxValue);
        }
    
        [Test]
        public void TestCtor()
        {
            try
            {
                MakeGetter(-1);
                Assert.Fail();
            }
            catch (ArgumentException ex)
            {
                // expected
            }
        }
    
        [Test]
        public void TestGet()
        {
            Assert.AreEqual(_bean.fieldStringArray[0], _getter.Get(_event));
            Assert.AreEqual(_bean.fieldStringArray[0], _getter.Get(_event, 0));
    
            Assert.IsNull(_getterOutOfBounds.Get(_event));
    
            try
            {
                _getter.Get(SupportEventBeanFactory.CreateObject(""));
                Assert.Fail();
            }
            catch (PropertyAccessException ex)
            {
                // expected
            }
        }
    
        private ArrayFieldPropertyGetter MakeGetter(int index)
        {
            FieldInfo field = typeof(SupportLegacyBean).GetField("fieldStringArray");
            return new ArrayFieldPropertyGetter(field, index, SupportEventAdapterService.Service);
        }
    }
}
