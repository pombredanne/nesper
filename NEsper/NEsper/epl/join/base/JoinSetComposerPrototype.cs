///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using com.espertech.esper.core.context.util;
using com.espertech.esper.view;

namespace com.espertech.esper.epl.join.@base
{
    /// <summary>
    /// Interface for a Prototype populating a join tuple result set from new data and
    /// old data for each stream.
    /// </summary>
    public interface JoinSetComposerPrototype
    {
        JoinSetComposerDesc Create(Viewable[] streamViews, bool isFireAndForget, AgentInstanceContext agentInstanceContext);
    }
}
