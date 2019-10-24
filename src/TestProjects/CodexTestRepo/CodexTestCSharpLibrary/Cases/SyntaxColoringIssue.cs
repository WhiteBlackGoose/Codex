using System;
using System.IO;
using System.Collections.Generic;

namespace CodexTestCSharpLibrary.Cases
{
    public class SyntaxColoringIssue
    {
        public void Test()
        {
            var map = new Dictionary<string, string>()
            {
                { "Key1", "One" },
                { "Key2", "Two" },
                // Punctuation between string literals should not have syntax coloring of string literals
            };
        }
    }
}
