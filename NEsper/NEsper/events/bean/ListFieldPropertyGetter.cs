///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Reflection;

using com.espertech.esper.client;
using com.espertech.esper.compat.collections;
using com.espertech.esper.events.vaevent;
using com.espertech.esper.util;

namespace com.espertech.esper.events.bean
{
    /// <summary>
    /// Getter for a list property backed by a field, identified by a given index, using vanilla reflection.
    /// </summary>
    public class ListFieldPropertyGetter 
        : BaseNativePropertyGetter
        , BeanEventPropertyGetter
        , EventPropertyGetterAndIndexed
    {
        private readonly FieldInfo _field;
        private readonly int _index;
    
        /// <summary>Constructor. </summary>
        /// <param name="field">is the field to use to retrieve a value from the object</param>
        /// <param name="index">is tge index within the array to get the property from</param>
        /// <param name="eventAdapterService">factory for event beans and event types</param>
        public ListFieldPropertyGetter(FieldInfo field, int index, EventAdapterService eventAdapterService)
            : base(eventAdapterService, TypeHelper.GetGenericFieldType(field, false), null)
        {
            _index = index;
            _field = field;
    
            if (index < 0)
            {
                throw new ArgumentException("Invalid negative index value");
            }
        }
    
        public Object Get(EventBean eventBean, int index)
        {
            return GetBeanPropInternal(eventBean.Underlying, index);
        }
    
        public Object GetBeanProp(Object o)
        {
            return GetBeanPropInternal(o, _index);
        }

        private Object GetBeanPropInternal(Object o, int index)
        {
            try
            {
                var value = _field.GetValue(o);
                var valueAsList = value as System.Collections.IList;
                if (valueAsList != null)
                {
                    return valueAsList.AtIndex(index, i => null);
                }

                return null;
            }
            catch (InvalidCastException e)
            {
                throw PropertyUtility.GetMismatchException(_field, o, e);
            }
            catch (Exception e)
            {
                throw new PropertyAccessException(e);
            }
        }

        public bool IsBeanExistsProperty(Object o)
        {
            return true; // Property exists as the property is not dynamic (unchecked)
        }
    
        public override Object Get(EventBean eventBean)
        {
            Object underlying = eventBean.Underlying;
            return GetBeanProp(underlying);
        }
    
        public override String ToString()
        {
            return "ListFieldPropertyGetter " +
                    " field=" + _field +
                    " index=" + _index;
        }
    
        public override bool IsExistsProperty(EventBean eventBean)
        {
            return true; // Property exists as the property is not dynamic (unchecked)
        }
    }
}
