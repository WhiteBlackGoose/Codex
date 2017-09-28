using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Codex.Analysis;
using Codex.Analysis.External;
using Codex.Analysis.Files;
using Codex.Analysis.FileSystems;
using Codex.Analysis.Managed;
using Codex.Analysis.Projects;
using Codex.Analysis.Xml;
using Codex.Import;
using Codex.Logging;
using Codex.ObjectModel;
using Codex.Storage;
using Mono.Options;

namespace Codex.Application
{
    class Program
    {
        static string elasticSearchServer = "http://localhost:9200";
        static bool finalize = true;
        static bool analysisOnly = false; // Set this to disable uploading to ElasticSearch
        static string repoName;
        static string rootDirectory;
        static string solutionPath;
        static bool interactive = false;
        static bool listIndices = false;
        static List<string> deleteIndices = new List<string>();

        static OptionSet options = new OptionSet
        {
            { "l|list", "List the indices.", n => listIndices = n != null },
            { "d=", "List the indices to delete.", n => deleteIndices.Add(n) },
            { "es|elasticsearch=", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
            { "n|name=", "Name of the repository.", n => repoName = AnalysisServices.GetSafeIndexName(n ?? string.Empty) },
            { "p|path=", "Path to the repo to analyze.", n => rootDirectory = n },
            { "s|solution=", "Optionally, path to the solution to analyze.", n => solutionPath = n },
            { "i|interactive", "Search newly indexed items.", n => interactive = n != null }
        };

        static void Main(string[] args)
        {
            var extras = options.Parse(args);

            Console.WriteLine("Server: " + elasticSearchServer);

            if (listIndices)
            {
                ListIndices();
                return;
            }

            if (deleteIndices.Count != 0)
            {
                DeleteIndices();

                Console.WriteLine("Remaining Indices:");
                ListIndices();
                return;
            }

            if (string.IsNullOrEmpty(rootDirectory)) throw new ArgumentException("Solution path is missing. Use -p to provide it.");
            if (string.IsNullOrEmpty(repoName)) throw new ArgumentException("Repository name is missing. Use -n to provide it.");
            if (string.IsNullOrEmpty(elasticSearchServer)) throw new ArgumentException("Elastic Search server URL is missing. Use -es to provide it.");

            try
            {
                RunRepoImporter();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error indexing {repoName}: {ex.Message}");
                Console.WriteLine(ex.ToString());
                Environment.Exit(-1);
            }

            if (interactive)
            {
                Search();
            }
        }

        private static void DeleteIndices()
        {
            ElasticsearchStorage storage = GetStorage();

            foreach (var index in deleteIndices)
            {
                Console.Write($"Deleting {index}... ");
                var deleted = storage.Provider.DeleteIndexAsync(index).GetAwaiter().GetResult().Succeeded;
                if (deleted)
                {
                    Console.WriteLine($"Success");
                }
                else
                {
                    Console.WriteLine($"Not Found");
                }
            }
        }

        private static void ListIndices()
        {
            ElasticsearchStorage storage = GetStorage();

            var indices = storage.Provider.GetIndicesAsync().GetAwaiter().GetResult();

            foreach (var index in indices)
            {
                Console.WriteLine(index.IndexName + (index.IsActive ? " (IsActive)" : ""));
            }
        }

        private static ElasticsearchStorage GetStorage()
        {
            return new ElasticsearchStorage(elasticSearchServer);
        }

        static void RunRepoImporter()
        {
            var targetIndexName = AnalysisServices.GetTargetIndexName(repoName);

            string[] file = new string[0];

            bool requireProjectsExist = true;

            string assembly = null;

            if (File.Exists(rootDirectory))
            {
                if (rootDirectory.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    rootDirectory.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    assembly = rootDirectory;
                }

                file = new[] { rootDirectory };
                rootDirectory = Path.GetDirectoryName(rootDirectory);
                requireProjectsExist = false;
            }
            Directory.CreateDirectory("output");
            using (StreamWriter writer = new StreamWriter(@"output\log.txt"))
            {
                Logger logger = new MultiLogger(
                    new ConsoleLogger(),
                    new TextLogger(TextWriter.Synchronized(writer)));

                

                FileSystem fileSystem = new CachingFileSystem(
                    new UnionFileSystem(file.Union(Enumerable.Repeat(solutionPath, String.IsNullOrEmpty(solutionPath) ? 0 : 1)),
                        new RootFileSystem(rootDirectory,
                            new MultiFileSystemFilter(
                                new DirectoryFileSystemFilter(@"\.", ".sln"),
                                new GitIgnoreFilter(),
                                new BinaryFileSystemFilter(new string[] { ".exe", ".dll", "*.blob", ".db" })
                                ))
                        {
                            DisableEnumeration = file.Length != 0
                        }));

                List<RepoProjectAnalyzer> projectAnalyzers = new List<RepoProjectAnalyzer>()
                {
                    //new MSBuildSolutionProjectAnalyzer()
                    new BinLogSolutionProjectAnalyzer()
                            {
                                RequireProjectFilesExist = requireProjectsExist
                            }
                };


                if (assembly != null)
                {
                    fileSystem = new FileSystem();

                    projectAnalyzers.Clear();
                    projectAnalyzers.Add(new MetadataAsSourceProjectAnalyzer(file));
                }

                IAnalysisTarget analysisTarget = null;
                if (analysisOnly)
                {
                    analysisTarget = new NullAnalysisTarget();
                }
                else
                {
                    ElasticsearchStorage storage = GetStorage();

                    logger.WriteLine("Removing repository");
                    ((IStorage)storage).RemoveRepository(targetIndexName).GetAwaiter().GetResult();

                    logger.WriteLine("Removed repository");

                    analysisTarget = storage;
                }

                RepositoryImporter importer = new RepositoryImporter(repoName,
                    rootDirectory,
                    new AnalysisServices(
                        targetIndexName,
                        fileSystem,
                        analyzers: new RepoFileAnalyzer[]
                        {
                        new SolutionFileAnalyzer(),
                        new MSBuildFileAnalyzer(),
                        new XmlFileAnalyzer(
                            ".ds",
                            ".xml",
                            ".config",
                            ".settings"),
                        })
                        //new ExternalRepoFileAnalyzer(new[] { @"d:\temp\Codex" }), // This indexer allows an external tool to write out codex spans for importing. We sill need to add support for a 'marker' file so we don't have to pass a folder.
                    {
                        AnalysisTarget = analysisTarget,
                        Logger = logger,
                    })
                {
                    AnalyzerDatas = projectAnalyzers.Select(a => new AnalyzerData() { Analyzer = a }).ToList()
                };

                importer.Import(finalizeImport: finalize).GetAwaiter().GetResult();
            }
        }

        private static void ComputeMissingProjects(ElasticsearchStorage storage)
        {
            storage.UpdateProjectsAsync(force: true).GetAwaiter().GetResult();

            var projectsResponse = storage.Provider.GetProjectsAsync(projectKind: nameof(ProjectKind.Source)).GetAwaiter().GetResult();

            var projectMap = storage.Projects;
            var projects = projectsResponse.Result;

            HashSet<string> processedReferences = new HashSet<string>();
            HashSet<string> hasProjectInfoProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> missingProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int i = 0;
            foreach (var project in projects)
            {
                Console.WriteLine($"Processing {project.Id} ({i++}/{projects.Count})");
                foreach (var referencedProject in project.ReferencedProjects)
                {
                    if (!processedReferences.Add(referencedProject.ProjectId))
                    {
                        continue;
                    }

                    if (!projectMap.ContainsKey(referencedProject.ProjectId)
                        && !referencedProject.ProjectId.EndsWith(".Fakes", StringComparison.OrdinalIgnoreCase))
                    {
                        if (missingProjects.Add(referencedProject.ProjectId))
                        {
                            Console.WriteLine("Missing: " + referencedProject.ProjectId);
                        }
                    }

                    var references = storage.GetReferencesToSymbolAsync(new Symbol()
                    {
                        ProjectId = referencedProject.ProjectId,
                        Kind = nameof(ReferenceKind.Definition)
                    }, 1).GetAwaiter().GetResult();

                    if (references.Total == 0
                        && !referencedProject.ProjectId.EndsWith(".Fakes", StringComparison.OrdinalIgnoreCase))
                    {
                        if (missingProjects.Add(referencedProject.ProjectId))
                        {
                            Console.WriteLine("Missing total: " + referencedProject.ProjectId);
                        }
                    }
                }
            }

            File.WriteAllLines("AllProjects.txt", projects.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).Select(
                p => $"{p.Id} (Has Definitions: {!missingProjects.Contains(p.Id)})"));
            File.WriteAllLines("MissingProjects.txt", missingProjects.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));

            return;
        }

        static void Search()
        {
            ElasticsearchStorage storage = new ElasticsearchStorage(elasticSearchServer);

            string[] repos = new string[] { repoName.ToLowerInvariant() };

            string line = null;
            Console.WriteLine("Please enter symbol short name: ");
            while ((line = Console.ReadLine()) != null)
            {
                if (line.Contains("`"))
                {
                    //results = storage.SearchAsync(repos, line, classification: null).Result;
                    var results = ((ElasticsearchStorage)storage).TextSearchAsync(repos, line.TrimStart('`')).Result;
                    Console.WriteLine($"Found {results.Count} matches");
                    foreach (var result in results)
                    {
                        Console.WriteLine($"{result.File} ({result.Span.LineNumber}, {result.Span.LineSpanStart})");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"{result.ReferringFilePath} in '{result.ReferringProjectId}'");
                        Console.ForegroundColor = ConsoleColor.Gray;

                        var bsf = storage.GetBoundSourceFileAsync(result.ReferringProjectId, result.ReferringFilePath).Result;

                        if (!string.IsNullOrEmpty(result.Span.LineSpanText))
                        {
                            Console.Write(result.Span.LineSpanText.Substring(0, result.Span.LineSpanStart));
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write(result.Span.LineSpanText.Substring(result.Span.LineSpanStart, result.Span.Length));
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine(result.Span.LineSpanText.Substring(result.Span.LineSpanStart + result.Span.Length));
                        }
                    }
                }
                else if (line.Contains("|"))
                {
                    var parts = line.Split('|');
                    var symbolId = SymbolId.UnsafeCreateWithValue(parts[0]);
                    var projectId = parts[1];

                    var results = storage.GetReferencesToSymbolAsync(repos, new Symbol() { Id = symbolId, ProjectId = projectId }).GetAwaiter().GetResult();

                    var relatedDefinitions = storage.GetRelatedDefinitions(repos,
                            symbolId.Value,
                            projectId).GetAwaiter().GetResult();

                    var definition = results.Entries
                        .Where(e => e.Span.Reference.ReferenceKind == nameof(ReferenceKind.Definition))
                        .Select(e => e.Span.Reference)
                        .FirstOrDefault();

                    if (definition != null)
                    {
                        var relatedDefs = storage.Provider.GetRelatedDefinitions(repos, definition.Id.Value, definition.ProjectId)
                            .GetAwaiter().GetResult();
                    }

                    Console.WriteLine($"Found {results.Total} matches");
                    foreach (var result in results.Entries)
                    {
                        Console.WriteLine($"{result.File} ({result.Span.LineNumber}, {result.Span.LineSpanStart})");
                        if (!string.IsNullOrEmpty(result.Span.LineSpanText))
                        {
                            Console.Write(result.Span.LineSpanText.Substring(0, result.Span.LineSpanStart));
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write(result.Span.LineSpanText.Substring(result.Span.LineSpanStart, result.Span.Length));
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine(result.Span.LineSpanText.Substring(result.Span.LineSpanStart + result.Span.Length));
                        }
                    }

                    if (results.Entries.Count != 0)
                    {
                        var result = results.Entries[0];
                        Console.WriteLine($"Retrieving source file {result.ReferringFilePath} in {result.ReferringProjectId}");

                        var stopwatch = Stopwatch.StartNew();
                        var sourceFile = storage.GetBoundSourceFileAsync(repos, result.ReferringProjectId, result.ReferringFilePath).GetAwaiter().GetResult();
                        var elapsed = stopwatch.Elapsed;

                        Console.WriteLine($"Retrieved source file in {elapsed.TotalMilliseconds} ms");

                        Console.WriteLine($"Source file has { sourceFile?.ClassificationSpans.Count ?? -1 } classifications");
                        if (sourceFile.ClassificationSpans != null)
                        {
                            ConcurrentDictionary<string, int> classificationCounters = new ConcurrentDictionary<string, int>();
                            foreach (var cs in sourceFile.ClassificationSpans)
                            {
                                classificationCounters.AddOrUpdate(cs.Classification, 1, (k, v) => v + 1);
                            }

                            foreach (var counter in classificationCounters)
                            {
                                Console.WriteLine($"Source file has {counter.Value} {counter.Key} classifications");
                            }
                        }

                        Console.WriteLine($"Source file has { sourceFile?.References.Count ?? -1 } references");
                        Console.WriteLine($"Source file has { sourceFile?.Definitions.Count ?? -1 } definitions");
                    }
                }
                else
                {
                    //results = storage.SearchAsync(repos, line, classification: null).Result;
                    var results = storage.SearchAsync(repos, line, null).Result;
                    Console.WriteLine($"Found {results.Total} matches");
                    foreach (var result in results.Entries)
                    {
                        Console.WriteLine($"{result.File} ({result.Span.LineNumber}, {result.Span.LineSpanStart})");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"{result.Symbol.Id}|{result.Symbol.ProjectId}");
                        Console.ForegroundColor = ConsoleColor.Gray;

                        var symbol = result.Symbol;
                        int index = result.DisplayName.IndexOf(symbol.ShortName);
                        if (index >= 0)
                        {
                            result.Span.LineSpanText = symbol.DisplayName;
                            result.Span.LineSpanStart = index;
                            result.Span.Length = symbol.ShortName.Length;
                        }

                        if (!string.IsNullOrEmpty(result.Span.LineSpanText))
                        {
                            Console.Write(result.Span.LineSpanText.Substring(0, result.Span.LineSpanStart));
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write(result.Span.LineSpanText.Substring(result.Span.LineSpanStart, result.Span.Length));
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine(result.Span.LineSpanText.Substring(result.Span.LineSpanStart + result.Span.Length));
                        }
                    }
                }
            }
        }
    }
}
