using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Integration.Tests
{
    [TestFixture]
    class CompilerTests
    {
        [Test]
        public void TestGetAssemblySymbol()
        {
            CSharpCompilation compilation = CSharpCompilation.Create("TestAssembly");
            var reference = MetadataReference.CreateFromFile(@"D:\Code\CloudStore\bin\ContentStore\Distributed\Debug\Microsoft.ContentStore.Grpc.dll");
            compilation = compilation.AddReferences(reference);

            var assemblySymbol =compilation.GetAssemblyOrModuleSymbol(reference);
        }
    }
}
