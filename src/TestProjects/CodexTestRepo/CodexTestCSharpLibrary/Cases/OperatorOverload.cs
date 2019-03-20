using System;
using System.IO;

namespace CodexTestCSharpLibrary.Cases
{
    public class OperationOverload
    {
        public static int operator +(OperationOverload other)
        {
            return 0 + 1;
        }

        public static TimeSpan Use()
        {
            return TimeSpan.MaxValue + TimeSpan.Zero;
        }
    }
}
