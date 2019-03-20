using Codex.Application;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Codex.Integration.Tests
{
    [TestFixture]
    public class CSharpAnalysisTests
    {
        /// <summary>
        /// This test doesn't actually verify anything. It just provides an easy way of viewing mappings
        /// </summary>
        [Test]
        public void TestAnalysis()
        {
            //
            (var root, var compilerArgumentsPath) = GetArgumentsPath();

            //compilerArgumentsPath = FilterArguments(compilerArgumentsPath, "SpecificReference.cs");
            compilerArgumentsPath = FilterArguments(compilerArgumentsPath, "OperatorOverload.cs");

            Environment.CurrentDirectory = Path.Combine(root, @"out\test");
            var outputPath = Path.Combine(root, @"out\test\TestAnalysis.cdx");
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            CodexApplication.Main(
                "dryRun",
                "-save", outputPath,
                "-p", root,
                "-noScan",
                "-repoUrl", "https://github.com/Ref12/Codex/",
                "-n", "CodexTestRepo",
                "-ca", compilerArgumentsPath);
        }

        private string FilterArguments(string compilerArgumentsPath, string includedFile)
        {
            var lines = File.ReadAllLines(compilerArgumentsPath);

            string filteredPath = Path.GetTempFileName();
            File.WriteAllLines(filteredPath, lines.Where(s => !s.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || s.EndsWith(includedFile, StringComparison.OrdinalIgnoreCase)));

            return filteredPath;
        }

        private static (string root, string path) GetArgumentsPath()
        {
            string path = null;
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (!string.IsNullOrEmpty(root))
            {
                path = Path.Combine(root, @"out\test\CodexTestCSharpLibrary\csc.args.txt");
                if (File.Exists(path))
                {
                    return (root, path);
                }

                root = Path.GetDirectoryName(root);
            }

            throw new FileNotFoundException();
        }
    }
}
