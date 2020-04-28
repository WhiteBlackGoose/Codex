using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Codex.Framework.Generation
{
    class Program
    {
        public static void Main(string[] args)
        {
            //CSharpCodeProvider CodeProvider = new CSharpCodeProvider();
            new Generator().Generate("");
        }
    }

    class ProgramTest
    {
        [Test]
        public void Main()
        {
            Program.Main(null);
        }
    }
}
