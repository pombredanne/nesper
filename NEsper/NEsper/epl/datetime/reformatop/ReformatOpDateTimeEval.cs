﻿///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

using com.espertech.esper.client;
using com.espertech.esper.compat;
using com.espertech.esper.epl.datetime.eval;
using com.espertech.esper.epl.expression.core;
using com.espertech.esper.epl.expression;
using com.espertech.esper.epl.expression.dot;

namespace com.espertech.esper.epl.datetime.reformatop
{
    public class ReformatOpDateTimeEval : ReformatOp
    {
        private readonly Type _returnType;
        private readonly DateTimeEval _dateTimeEval;
        private readonly TimeZoneInfo _timeZone;

        public ReformatOpDateTimeEval(DateTimeEval dateTimeEval, Type returnType, TimeZoneInfo timeZone)
        {
            _dateTimeEval = dateTimeEval;
            _returnType = returnType;
            _timeZone = timeZone;
        }

        public Object Evaluate(long ts, EventBean[] eventsPerStream, bool newData, ExprEvaluatorContext exprEvaluatorContext)
        {
            return _dateTimeEval.Invoke(ts.TimeFromMillis(_timeZone));
        }

        public object Evaluate(DateTimeOffset dateTime, EventBean[] eventsPerStream, bool newData, ExprEvaluatorContext exprEvaluatorContext)
        {
            return _dateTimeEval.Invoke(dateTime.TranslateTo(_timeZone));
        }

        public Type ReturnType
        {
            get { return _returnType; }
        }

        public ExprDotNodeFilterAnalyzerDesc GetFilterDesc(EventType[] typesPerStream, DatetimeMethodEnum currentMethod, ICollection<ExprNode> currentParameters, ExprDotNodeFilterAnalyzerInput inputDesc)
        {
            return null;
        }
    }
}
