///////////////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2006-2017 Esper Team. All rights reserved.                           /
// http://esper.codehaus.org                                                          /
// ---------------------------------------------------------------------------------- /
// The software in this package is published under the terms of the GPL license       /
// a copy of which has been included with this distribution in the license.txt file.  /
///////////////////////////////////////////////////////////////////////////////////////

using System;

using com.espertech.esper.compat.collections;

namespace com.espertech.esper.regression.rowrecog
{
    public class SupportTestCaseItem
    {
        public SupportTestCaseItem(String testdata, String[] expected)
        {
            Testdata = testdata;
            Expected = expected;
        }

        public string Testdata { get; private set; }

        public string[] Expected { get; private set; }

        public override string ToString()
        {
            return string.Format("Testdata: {0}, Expected: {1}", Testdata, Expected.Render());
        }
    }
}
