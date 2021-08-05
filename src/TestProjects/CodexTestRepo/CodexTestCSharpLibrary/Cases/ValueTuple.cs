using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodexTestCSharpLibrary.Cases.InterfaceMemberImplementation
{
    public class ValueTupleTest
    {
        private (int tupleField1, bool tupleField2) Run()
        {
            (int tupleField1, bool tupleField2) result = (tupleField1: 0, tupleField2: false);
            (int, bool) otherTuple = (231, true);

            var b = result.tupleField1;
            var c = result.tupleField2;

            return result;
        }
    }
}
