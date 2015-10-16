///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

using com.espertech.esper.client;
using com.espertech.esper.collection;
using com.espertech.esper.epl.core;
using com.espertech.esper.epl.expression.core;
using com.espertech.esper.epl.expression;

namespace com.espertech.esper.epl.view
{
    /// <summary>Factory for output processing views. </summary>
    public class OutputProcessViewBaseCallback : OutputProcessViewBase
    {
        private readonly OutputProcessViewCallback _callback;

        public OutputProcessViewBaseCallback(ResultSetProcessor resultSetProcessor, OutputProcessViewCallback callback)
            : base(resultSetProcessor)
        {
            _callback = callback;
        }

        public override int NumChangesetRows
        {
            get { return 0; }
        }

        public override IEnumerator<EventBean> GetEnumerator()
        {
            return OutputStrategyUtil.GetEnumerator(
                base.JoinExecutionStrategy,
                base.ResultSetProcessor,
                Parent,
                false);
        }

        public override void Process(ISet<MultiKey<EventBean>> newEvents, ISet<MultiKey<EventBean>> oldEvents, ExprEvaluatorContext exprEvaluatorContext)
        {
            UniformPair<EventBean[]> pair = ResultSetProcessor.ProcessJoinResult(newEvents, oldEvents, false);
            _callback.OutputViaCallback(pair.First);
        }

        public override void Terminated()
        {
        }

        public override void Update(EventBean[] newData, EventBean[] oldData)
        {
            UniformPair<EventBean[]> pair = ResultSetProcessor.ProcessViewResult(newData, oldData, false);
            _callback.OutputViaCallback(pair.First);
        }
    }
}