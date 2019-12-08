using Codex.Application;
using Codex.ElasticSearch;
using Codex.ElasticSearch.Legacy.Bridge;
using Codex.Sdk.Search;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
            (var root, var compilerArgumentsPath) = GetArgumentsPath();

            //compilerArgumentsPath = FilterArguments(compilerArgumentsPath, "SpecificReference.cs");
            //compilerArgumentsPath = FilterArguments(compilerArgumentsPath, "OperatorOverload.cs");
            //compilerArgumentsPath = FilterArguments(compilerArgumentsPath, "MethodGroup.cs");
            compilerArgumentsPath = FilterArguments(compilerArgumentsPath, "NormalizeStringClassification.cs");
            //compilerArgumentsPath = FilterArguments(compilerArgumentsPath, "DerivedImplementation.Issue159.cs");

            Environment.CurrentDirectory = Path.Combine(root, @"out\test");
            var outputPath = Path.Combine(root, @"out\test\TestAnalysis.cdx");
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            using (Features.AddDefinitionForInheritedInterfaceImplementations.EnableLocal())
            {
                new CodexApplication().Run(
                    "dryRun",
                    "-save", outputPath,
                    "-p", root,
                    "-noScan",
                    "-repoUrl", "https://github.com/Ref12/Codex/",
                    "-n", "CodexTestRepo",
                    "-ca", compilerArgumentsPath);
            }
        }

        /// <summary>
        /// This test doesn't actually verify anything. It just provides an easy way of viewing mappings
        /// </summary>
        [Test]
        public void TestHelixAnalysis()
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

            new CodexApplication().Run(
                "dryRun",
                "-save", outputPath,
                "-p", root,
                "-projectMode",
                "-disableParallelFiles",
                "-repoUrl", "https://github.com/Ref12/Codex/",
                "-n", "CodexTestRepo",
                "-ca", @"D:\Code\Helix.Ide\obj\cdx\Microsoft.Ide.Common.4C6B42D\csc.args.txt",
                "-projectDataSuffix", "4C6B42D");
        }

        [Test]
        public async Task TestOptimizeAnalysis()
        {
            //new CodexApplication().Run(
            //    "load",
            //    "-clean",
            //    //"-save", outputPath,
            //    "-d", @"D:\temp\cdx\store.zip",
            //    "-save", @"D:\temp\cdx\store2.zip");

            //new CodexApplication().Run(
            //    "load",
            //    "-n", "dominotest",
            //    "-es", "http://ddindex:9125",
            //    "-d", @"C:\Users\lancec\Downloads\store (4).zip");

            var codex = new LegacyElasticSearchCodex(new LegacyElasticSearchStoreConfiguration()
            {
                Endpoint = "http://ddindex:9125",
            });

            var result = await codex.SearchAsync(new SearchArguments()
            {
                SearchString = "class Scheduler",
                TextSearch = true,
                RepositoryScopeId = "dominotest.191208.001440"
            });

            var primaryResult = result.Result.Hits.First().Definition;

            var path = await codex.GetFirstDefinitionFilePath(primaryResult.ProjectId, primaryResult.Id.Value);

            var file = await codex.GetSourceAsync(new GetSourceArguments()
            {
                ProjectId = primaryResult.ProjectId,
                ProjectRelativePath = path,
                RepositoryScopeId = "dominotest"
            });

            var classifications = file.Result.Classifications.ToList();
        }

        [Test]
        public void TestSaveAnalysis()
        {
            //
            (var root, var compilerArgumentsPath) = GetArgumentsPath();

            Environment.CurrentDirectory = Path.Combine(root, @"out\test");
            var outputPath = Path.Combine(root, @"out\test\TestAnalysis.cdx");
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            new CodexApplication().Run(
                "index",
                //"-save", outputPath,
                "-es", "http://localhost:9200",
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
