using System;
using System.IO;
using System.Collections.Generic;

namespace CodexTestCSharpLibrary.Cases
{
    public class NormalizeStringClassification
    {
        Func<TimeSpan, string> formatTime = (t) => string.Format("{0:hh\\:mm\\:ss}", t);
    }
}
