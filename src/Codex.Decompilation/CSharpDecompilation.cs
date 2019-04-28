using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using System;
using System.Collections.Generic;
using System.Text;

namespace Codex.Decompilation
{
    public class CSharpDecompilation
    {
        public CSharpDecompilation(string assemblyFile)
        {

        }

        public void Decompile(string assemblyFileName)
        {
            var decompiler = new WholeProjectDecompiler();

            var module = new PEFile(assemblyFileName);
            var resolver = new UniversalAssemblyResolver(assemblyFileName, false, module.Reader.DetectTargetFrameworkId());
            //foreach (var path in referencePaths)
            //{
            //    resolver.AddSearchDirectory(path);
            //}
            decompiler.AssemblyResolver = resolver;
           // decompiler.DecompileProject(module, outputDirectory);
        }

    }
}
