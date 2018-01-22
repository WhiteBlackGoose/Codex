using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Codex.Analysis;
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
using Codex.Sdk.Search;
using Codex.ElasticSearch.Store;

namespace Codex.Application
{
    class CodexApplication
    {
        static string elasticSearchServer = "http://localhost:9200";
        static bool finalize = true;
        static bool analysisOnly = false; // Set this to disable uploading to ElasticSearch
        static string repoName;
        static string rootDirectory;
        static string repoUrl;
        static string saveDirectory;
        static string binLogSearchDirectory;
        static string solutionPath;
        static bool interactive = false;
        static ICodexStore store = Placeholder.Value<ICodexStore>("Create store (FileSystem | Elasticsearch)");
        static ElasticSearchService service;
        static bool test = false;
        static bool update = false;
        static List<string> deleteIndices = new List<string>();

        static Dictionary<string, (Action act, OptionSet options)> actions = new Dictionary<string, (Action, OptionSet)>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "index",
                (
                    new Action(() => Index()),
                    new OptionSet
                    {
                        { "es|elasticsearch=", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
                        { "save=", "Saves the analysis information to the given directory.", n => saveDirectory = n },
                        { "test", "Indicates that save should use test mode which disables optimization.", n => test = n != null },
                        { "n|name=", "Name of the repository.", n => repoName = AnalysisServices.GetSafeIndexName(n ?? string.Empty) },
                        { "p|path=", "Path to the repo to analyze.", n => rootDirectory = n },
                        { "repoUrl", "The URL of the repository being indexed", n => repoUrl = n },
                        { "bld|binLogSearchDirectory", "The directory to search for binlog files", n => binLogSearchDirectory = n },
                        { "s|solution", "Optionally, path to the solution to analyze.", n => solutionPath = n },
                        { "i|interactive", "Search newly indexed items.", n => interactive = n != null }
                    }
                )
            },
            {
                "dryRun",
                (
                    new Action(() => Analyze()),
                    new OptionSet
                    {
                        { "n|name=", "Name of the project.", n => repoName = n },
                        { "p|path=", "Path to the repo to analyze.", n => rootDirectory = n },
                        { "s|solution", "Optionally, path to the solution to analyze.", n => solutionPath = n },
                    }
                )
            },
            {
                "delete",
                (
                    new Action(() => DeleteIndices()),
                    new OptionSet
                    {
                        { "es|elasticsearch", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
                        { "d=", "List the indices to delete.", n => deleteIndices.Add(n) },
                    }
                )
            },
            {
                "load",
                (
                    new Action(() => Load()),
                    new OptionSet
                    {
                        { "es|elasticsearch=", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
                        { "u", "Updates the analysis data (in place).", n => update = n != null },
                        { "d=", "The directory containing analysis data to load.", n => saveDirectory = n },
                    }
                )
            },
            {
                "search",
                (
                    new Action(() => Search()),
                    new OptionSet
                    {
                        { "es|elasticsearch", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
                        { "n|name=", "Name of the project to search.", n => repoName = n },
                    }
                )
            },
            {
                "list",
                (
                    new Action(() => ListIndices()),
                    new OptionSet
                    {
                        { "es|elasticsearch", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
                    }
                )
            }
        };

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                WriteHelpText();
                return;
            }

            var remaining = args.Skip(1).ToArray();
            var verb = args[0].ToLowerInvariant();
            if (actions.TryGetValue(verb, out var action))
            {
                action.options.Parse(remaining);
                action.act();
            }
            else
            {
                Console.Error.WriteLine($"Invalid verb '{verb}'");
                WriteHelpText();
            }
        }

        private static void WriteHelpText()
        {
            foreach (var actionEntry in actions)
            {
                Console.WriteLine($"codex {actionEntry.Key} {{options}}");
                Console.WriteLine("Options:");
                actionEntry.Value.options.WriteOptionDescriptions(Console.Out);
                Console.WriteLine();
            }
        }

        static void Delete()
        {
            if (deleteIndices.Count != 0)
            {
                DeleteIndices();

                Console.WriteLine("Remaining Indices:");
                ListIndices();
                return;
            }
        }

        static void Analyze()
        {
            analysisOnly = true;
            Index();
        }

        static void Index()
        {
            if (String.IsNullOrEmpty(rootDirectory)) throw new ArgumentException("Root path is missing. Use -p to provide it.");
            if (String.IsNullOrEmpty(repoName)) throw new ArgumentException("Project name is missing. Use -n to provide it.");
            if (saveDirectory == null)
            {
                InitService();

                store = service.CreateStoreAsync(new ElasticSearchStoreConfiguration()
                {
                    // TODO: Remove these for production. Indices should be created by a single setup process
                    CreateIndices = true,
                    ClearIndicesBeforeUse = true,
                    ShardCount = 2,
                    Prefix = "apptest"
                }).GetAwaiter().GetResult();
            }
            else
            {
                store = new DirectoryCodexStore(saveDirectory) { DisableOptimization = test };
            }

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

        private static void DeleteIndices()
        {
            InitService();

            foreach (var index in deleteIndices)
            {
                Console.Write($"Deleting {index}... ");
                var deleted = service.DeleteIndexAsync(index).GetAwaiter().GetResult();
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

        private static void Load()
        {
            Task.Run(async () =>
            {
                if (String.IsNullOrEmpty(saveDirectory)) throw new ArgumentException("Load directory must be specified. Use -d to provide it.");

                if (!update)
                {
                    InitService();
                    store = await service.CreateStoreAsync(new ElasticSearchStoreConfiguration());
                }
                else
                {
                    store = new DirectoryCodexStore(saveDirectory) { Clean = false };
                }

                Directory.CreateDirectory("output");
                using (StreamWriter writer = new StreamWriter(@"output\log.txt"))
                {
                    Logger logger = new MultiLogger(
                        new ConsoleLogger(),
                        new TextLogger(TextWriter.Synchronized(writer)));
                    var loadDirectoryStore = new DirectoryCodexStore(saveDirectory, logger);
                    await loadDirectoryStore.ReadAsync(store);
                }

            }).GetAwaiter().GetResult();
        }

        private static void ListIndices()
        {
            InitService();

            var indices = service.GetIndicesAsync().GetAwaiter().GetResult();

            foreach (var index in indices)
            {
                Console.WriteLine(index.IndexName + (index.IsActive ? " (IsActive)" : ""));
            }
        }

        private static void InitService()
        {
            if (String.IsNullOrEmpty(elasticSearchServer)) throw new ArgumentException("Elastic Search server URL is missing. Use -es to provide it.");

            service = new ElasticSearchService(new ElasticSearchServiceConfiguration(elasticSearchServer));
        }

        static async Task RunRepoImporter()
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

                                // Filter out files from being indexed specified by the .cdxignore file
                                // This is used to ignore files which are not specified in the .gitignore files
                                new GitIgnoreFilter("cdx.ignore"),
                                new GitIgnoreFilter(),
                                new BinaryFileSystemFilter(new string[] { ".exe", ".dll", "*.blob", ".db" })
                                ))
                        {
                            DisableEnumeration = file.Length != 0
                        }));

                Func<string, string> binLogFinder = null;

                List<RepoProjectAnalyzer> projectAnalyzers = new List<RepoProjectAnalyzer>()
                {
                    //new MSBuildSolutionProjectAnalyzer()
                    new BinLogSolutionProjectAnalyzer(logger, binLogSearchDirectory: binLogSearchDirectory)
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
                        new Repository(repoName)
                        {
                            SourceControlWebAddress = repoUrl,
                        },
                        new Commit()
                        {
                            RepositoryName = repoName,
                            CommitId = targetIndexName,
                            DateUploaded = DateTime.UtcNow,
                        },
                        new Branch()
                        {
                            HeadCommitId = targetIndexName,
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

        static void Search()
        {
            SearchAsync().Wait();
        }

        static async Task SearchAsync()
        {
            InitService();

            var codex = await service.CreateCodexAsync(new ElasticSearchStoreConfiguration()
            {
            });

            string line;
            while ((line = Console.ReadLine()) != null)
            {
                var response = await codex.SearchAsync(new SearchArguments() { SearchString = line });
                var results = response.Result;
                Console.WriteLine($"Found {results.Total} matches");
                foreach (var searchResult in results.Hits)
                {
                    var result = searchResult.TextLine;
                    Console.WriteLine($@"\\{result.RepositoryName}\{result.RepoRelativePath}");
                    Console.WriteLine($"{result.ProjectId}:{result.ProjectRelativePath} ({result.TextSpan.LineIndex}, {result.TextSpan.LineSpanStart})");

                    if (!string.IsNullOrEmpty(result.TextSpan.LineSpanText))
                    {
                        Console.Write(result.TextSpan.LineSpanText.Substring(0, result.TextSpan.LineSpanStart));
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write(result.TextSpan.LineSpanText.Substring(result.TextSpan.LineSpanStart, result.TextSpan.Length));
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine(result.TextSpan.LineSpanText.Substring(result.TextSpan.LineSpanStart + result.TextSpan.Length));
                    }
                }

                if (results.Total == 0)
                {
                    foreach (var rawQuery in response.RawQueries)
                    {
                        Console.WriteLine(rawQuery);
                    }
                }
            }

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
