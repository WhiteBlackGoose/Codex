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
using Codex.ElasticSearch;
using System.Threading.Tasks;

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
        static ElasticSearchStore store = Placeholder.Value<ElasticSearchStore>("Create store (FileSystem | Elasticsearch)");
        static ElasticSearchService service;

        static OptionSet indexOptions = new OptionSet
        {
            { "es|elasticsearch=", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
            { "n|name=", "Name of the project.", n => repoName = n },
            { "p|path=", "Path to the repo to analyze.", n => rootDirectory = n },
            { "s|solution", "Optionally, path to the solution to analyze.", n => solutionPath = n },
            { "i|interactive", "Search newly indexed items.", n => interactive = n != null }
        };

        static OptionSet analysisOptions = new OptionSet
        {
            { "es|elasticsearch", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
            { "n|name=", "Name of the project.", n => repoName = n },
            { "p|path=", "Path to the repo to analyze.", n => rootDirectory = n },
            { "s|solution", "Optionally, path to the solution to analyze.", n => solutionPath = n },
        };

        static OptionSet searchOptions = new OptionSet
        {
            { "es|elasticsearch=", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
            { "n|name=", "Name of the project to search.", n => repoName = n },
        };

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                WriteHelpText();
                return;
            }

            var remaining = args.Skip(1).ToArray();
            switch (args[0].ToLowerInvariant())
            {
                case "index":
                    Index(remaining);
                    return;
                case "search":
                    Search(remaining);
                    return;
                case "dryRun":
                    analysisOnly = true;
                    Index(remaining);
                    return;
                default:
                    Console.Error.WriteLine($"Invalid verb '{args[0]}'");
                    WriteHelpText();
                    return;
            }
        }

        private static void WriteHelpText()
        {
            Console.WriteLine("codex index {options}");
            Console.WriteLine("Options:");
            indexOptions.WriteOptionDescriptions(Console.Out);
            Console.WriteLine();
            Console.WriteLine("codex search {options}");
            Console.WriteLine("Options:");
            searchOptions.WriteOptionDescriptions(Console.Out);
            Console.WriteLine("codex dryrun {options}");
            Console.WriteLine("Options:");
            analysisOptions.WriteOptionDescriptions(Console.Out);
            Console.WriteLine();
        }

        static void Index(string[] args)
        {
            var extras = indexOptions.Parse(args);
            if (String.IsNullOrEmpty(rootDirectory)) throw new ArgumentException("Root path is missing. Use -p to provide it.");
            if (String.IsNullOrEmpty(repoName)) throw new ArgumentException("Project name is missing. Use -n to provide it.");
            if (String.IsNullOrEmpty(elasticSearchServer)) throw new ArgumentException("Elastic Search server URL is missing. Use -es to provide it.");

            service = new ElasticSearchService(new ElasticSearchServiceConfiguration(elasticSearchServer));

            try
            {
                RunRepoImporter().GetAwaiter().GetResult();
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

        static async Task RunRepoImporter()
        {
            var targetIndexName = AnalysisServices.GetTargetIndexName(repoName);

            store = await service.CreateStoreAsync(new ElasticSearchStoreConfiguration()
            {
                CreateIndices = true,
                ShardCount = 5,
                Prefix = "test."
            });

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
                    new MSBuildSolutionProjectAnalyzer()
                    //new ManagedSolutionProjectAnalyzer()
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

                ICodexRepositoryStore analysisTarget = null;
                if (analysisOnly)
                {
                    analysisTarget = new NullCodexRepositoryStore();
                }
                else
                {
                    Placeholder.Todo("Populate commit/repo/branch with full set of real values");
                    analysisTarget = await store.CreateRepositoryStore(
                        new Repository(repoName),
                        new Commit()
                        {
                            RepositoryName = repoName,
                            CommitId = targetIndexName,
                            DateUploaded = DateTime.UtcNow,
                        },
                        new Branch()
                        {
                            CommitId = targetIndexName,
                        });
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
                        RepositoryStore = analysisTarget,
                        Logger = logger,
                    })
                {
                    AnalyzerDatas = projectAnalyzers.Select(a => new AnalyzerData() { Analyzer = a }).ToList()
                };

                await importer.Import(finalizeImport: finalize);
            }
        }

        //private static void ComputeMissingProjects(ElasticsearchStorage storage)
        //{
        //    storage.UpdateProjectsAsync(force: true).GetAwaiter().GetResult();

        //    var projectsResponse = storage.Provider.GetProjectsAsync(projectKind: nameof(ProjectKind.Source)).GetAwaiter().GetResult();

        //    var projectMap = storage.Projects;
        //    var projects = projectsResponse.Result;

        //    HashSet<string> processedReferences = new HashSet<string>();
        //    HashSet<string> hasProjectInfoProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        //    HashSet<string> missingProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        //    int i = 0;
        //    foreach (var project in projects)
        //    {
        //        Console.WriteLine($"Processing {project.Id} ({i++}/{projects.Count})");
        //        foreach (var referencedProject in project.ReferencedProjects)
        //        {
        //            if (!processedReferences.Add(referencedProject.ProjectId))
        //            {
        //                continue;
        //            }

        //            if (!projectMap.ContainsKey(referencedProject.ProjectId)
        //                && !referencedProject.ProjectId.EndsWith(".Fakes", StringComparison.OrdinalIgnoreCase))
        //            {
        //                if (missingProjects.Add(referencedProject.ProjectId))
        //                {
        //                    Console.WriteLine("Missing: " + referencedProject.ProjectId);
        //                }
        //            }

        //            var references = storage.GetReferencesToSymbolAsync(new Symbol()
        //            {
        //                ProjectId = referencedProject.ProjectId,
        //                Kind = nameof(ReferenceKind.Definition)
        //            }, 1).GetAwaiter().GetResult();

        //            if (references.Total == 0
        //                && !referencedProject.ProjectId.EndsWith(".Fakes", StringComparison.OrdinalIgnoreCase))
        //            {
        //                if (missingProjects.Add(referencedProject.ProjectId))
        //                {
        //                    Console.WriteLine("Missing total: " + referencedProject.ProjectId);
        //                }
        //            }
        //        }
        //    }

        //    File.WriteAllLines("AllProjects.txt", projects.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).Select(
        //        p => $"{p.Id} (Has Definitions: {!missingProjects.Contains(p.Id)})"));
        //    File.WriteAllLines("MissingProjects.txt", missingProjects.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));

        //    return;
        //}

        static void Search(string[] args = null)
        {
            //ElasticsearchStorage storage = new ElasticsearchStorage(elasticSearchServer);

            //string[] repos = new string[] { repoName.ToLowerInvariant() };

            //string line = null;
            //Console.WriteLine("Please enter symbol short name: ");
            //while ((line = Console.ReadLine()) != null)
            //{
            //    if (line.Contains("`"))
            //    {
            //        //results = storage.SearchAsync(repos, line, classification: null).Result;
            //        var results = ((ElasticsearchStorage)storage).TextSearchAsync(repos, line.TrimStart('`')).Result;
            //        Console.WriteLine($"Found {results.Count} matches");
            //        foreach (var result in results)
            //        {
            //            Console.WriteLine($"{result.File} ({result.Span.LineNumber}, {result.Span.LineSpanStart})");
            //            Console.ForegroundColor = ConsoleColor.Green;
            //            Console.WriteLine($"{result.ReferringFilePath} in '{result.ReferringProjectId}'");
            //            Console.ForegroundColor = ConsoleColor.Gray;

            //            var bsf = storage.GetBoundSourceFileAsync(result.ReferringProjectId, result.ReferringFilePath).Result;

            //            if (!string.IsNullOrEmpty(result.Span.LineSpanText))
            //            {
            //                Console.Write(result.Span.LineSpanText.Substring(0, result.Span.LineSpanStart));
            //                Console.ForegroundColor = ConsoleColor.Yellow;
            //                Console.Write(result.Span.LineSpanText.Substring(result.Span.LineSpanStart, result.Span.Length));
            //                Console.ForegroundColor = ConsoleColor.Gray;
            //                Console.WriteLine(result.Span.LineSpanText.Substring(result.Span.LineSpanStart + result.Span.Length));
            //            }
            //        }
            //    }
            //    else if (line.Contains("|"))
            //    {
            //        var parts = line.Split('|');
            //        var symbolId = SymbolId.UnsafeCreateWithValue(parts[0]);
            //        var projectId = parts[1];

            //        var results = storage.GetReferencesToSymbolAsync(repos, new Symbol() { Id = symbolId, ProjectId = projectId }).GetAwaiter().GetResult();

            //        var relatedDefinitions = storage.GetRelatedDefinitions(repos,
            //                symbolId.Value,
            //                projectId).GetAwaiter().GetResult();

            //        var definition = results.Entries
            //            .Where(e => e.Span.Reference.ReferenceKind == nameof(ReferenceKind.Definition))
            //            .Select(e => e.Span.Reference)
            //            .FirstOrDefault();

            //        if (definition != null)
            //        {
            //            var relatedDefs = storage.Provider.GetRelatedDefinitions(repos, definition.Id.Value, definition.ProjectId)
            //                .GetAwaiter().GetResult();
            //        }

            //        Console.WriteLine($"Found {results.Total} matches");
            //        foreach (var result in results.Entries)
            //        {
            //            Console.WriteLine($"{result.File} ({result.Span.LineNumber}, {result.Span.LineSpanStart})");
            //            if (!string.IsNullOrEmpty(result.Span.LineSpanText))
            //            {
            //                Console.Write(result.Span.LineSpanText.Substring(0, result.Span.LineSpanStart));
            //                Console.ForegroundColor = ConsoleColor.Yellow;
            //                Console.Write(result.Span.LineSpanText.Substring(result.Span.LineSpanStart, result.Span.Length));
            //                Console.ForegroundColor = ConsoleColor.Gray;
            //                Console.WriteLine(result.Span.LineSpanText.Substring(result.Span.LineSpanStart + result.Span.Length));
            //            }
            //        }

            //        if (results.Entries.Count != 0)
            //        {
            //            var result = results.Entries[0];
            //            Console.WriteLine($"Retrieving source file {result.ReferringFilePath} in {result.ReferringProjectId}");

            //            var stopwatch = Stopwatch.StartNew();
            //            var sourceFile = storage.GetBoundSourceFileAsync(repos, result.ReferringProjectId, result.ReferringFilePath).GetAwaiter().GetResult();
            //            var elapsed = stopwatch.Elapsed;

            //            Console.WriteLine($"Retrieved source file in {elapsed.TotalMilliseconds} ms");

            //            Console.WriteLine($"Source file has { sourceFile?.Classifications.Count ?? -1 } classifications");
            //            if (sourceFile.Classifications != null)
            //            {
            //                ConcurrentDictionary<string, int> classificationCounters = new ConcurrentDictionary<string, int>();
            //                foreach (var cs in sourceFile.Classifications)
            //                {
            //                    classificationCounters.AddOrUpdate(cs.Classification, 1, (k, v) => v + 1);
            //                }

            //                foreach (var counter in classificationCounters)
            //                {
            //                    Console.WriteLine($"Source file has {counter.Value} {counter.Key} classifications");
            //                }
            //            }

            //            Console.WriteLine($"Source file has { sourceFile?.References.Count ?? -1 } references");
            //            Console.WriteLine($"Source file has { sourceFile?.Definitions.Count ?? -1 } definitions");
            //        }
            //    }
            //    else
            //    {
            //        //results = storage.SearchAsync(repos, line, classification: null).Result;
            //        var results = storage.SearchAsync(repos, line, null).Result;
            //        Console.WriteLine($"Found {results.Total} matches");
            //        foreach (var result in results.Entries)
            //        {
            //            Console.WriteLine($"{result.File} ({result.Span.LineNumber}, {result.Span.LineSpanStart})");
            //            Console.ForegroundColor = ConsoleColor.Green;
            //            Console.WriteLine($"{result.Symbol.Id}|{result.Symbol.ProjectId}");
            //            Console.ForegroundColor = ConsoleColor.Gray;

            //            var symbol = result.Symbol;
            //            int index = result.DisplayName.IndexOf(symbol.ShortName);
            //            //if (index >= 0)
            //            //{
            //            //    result.Span.LineSpanText = symbol.DisplayName;
            //            //    result.Span.LineSpanStart = index;
            //            //    result.Span.Length = symbol.ShortName.Length;
            //            //}

            //            //if (!string.IsNullOrEmpty(result.Span.LineSpanText))
            //            //{
            //            //    Console.Write(result.Span.LineSpanText.Substring(0, result.Span.LineSpanStart));
            //            //    Console.ForegroundColor = ConsoleColor.Yellow;
            //            //    Console.Write(result.Span.LineSpanText.Substring(result.Span.LineSpanStart, result.Span.Length));
            //            //    Console.ForegroundColor = ConsoleColor.Gray;
            //            //    Console.WriteLine(result.Span.LineSpanText.Substring(result.Span.LineSpanStart + result.Span.Length));
            //            //}
            //        }
            //    }
            //}
        }
    }
}
