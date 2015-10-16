///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2015 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////


using com.espertech.esper.client;

namespace com.espertech.esper.view
{
    /// <summary>
    /// A streams is a conduct for incoming events. Incoming data is placed into streams for consumption by queries.
    /// </summary>
    public interface EventStream : Viewable
    {
        /// <summary> Set a new event onto the stream.</summary>
        /// <param name="theEvent">to insert</param>
        void Insert(EventBean theEvent);

        /// <summary>
        /// Insert new events onto the stream.
        /// </summary>
        /// <param name="events">to insert</param>
        void Insert(EventBean[] events);
    }
}
