using CommandLine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.IO.Compression;
using CommandLine.Text;
using Mono.Cecil;

namespace Codex.Rewriter
{
    /// <summary>
    /// Rewrites ICSharpCode.Decompiler.dll
    /// </summary>
    public class CSharpCodeDecompilerAssemblyRewriter
    {
        public CSharpCodeDecompilerAssemblyRewriter(string assemblyPath)
        {
            Assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
        }

        public void Rewrite()
        {
            //MainModule.TryGetTypeReference("");
            var wholeProjectDecompilerType = GetType("ICSharpCode.Decompiler.CSharp.WholeProjectDecompiler");
        }

        // Common members

        private MethodDefinition GetMethod(string typeName, string methodName)
        {
            TypeDefinition typeDefinition = GetType(typeName);
            return typeDefinition.Methods.Single(m => m.Name == methodName);
        }

        private TypeDefinition GetType(string typeName)
        {
            return MainModule.GetType(typeName);
        }

        public AssemblyDefinition Assembly { get; }
        public ModuleDefinition MainModule => Assembly.MainModule;
    }
}
