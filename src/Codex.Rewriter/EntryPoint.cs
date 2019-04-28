using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Rewriter
{
    [TestFixture]
    public class EntryPoint
    {
        [Test]
        public void Run()
        {
            var path = @"C:\Users\lancec\.nuget\packages\icsharpcode.decompiler\5.0.0.4688-preview1\lib\netstandard2.0\ICSharpCode.Decompiler.dll";
            var rewriter = new CSharpCodeDecompilerAssemblyRewriter(path);
            rewriter.Rewrite();
        }
    }
}
