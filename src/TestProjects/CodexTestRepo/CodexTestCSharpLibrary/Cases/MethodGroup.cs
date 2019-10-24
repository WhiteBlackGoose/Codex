using System;
using System.IO;
using System.Collections.Generic;

namespace CodexTestCSharpLibrary.Cases
{
    public class MethodGroup
    {
        public event Action TestEvent;
        public Action TestField;
        
        public void Test()
        {
            TestEvent += Test;
            TestField = Test;
            TestField += Test;
        }
    }
}
