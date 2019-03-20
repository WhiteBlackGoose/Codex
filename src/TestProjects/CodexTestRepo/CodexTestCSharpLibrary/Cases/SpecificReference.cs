using System.IO;

namespace CodexTestCSharpLibrary.Cases
{
    public class SpecificReference
    {
        public static void Test()
        {
            // Specific dispose method of StringWriter
            new StringWriter().Dispose();
        }
    }
}
