using Codex.Analysis;
using Codex.Analysis.Files;
using Codex.Analysis.FileSystems;
using Codex.Analysis.Managed;
using Codex.Analysis.Projects;
using Codex.Analysis.Xml;
using Codex.ElasticSearch;
using Codex.ElasticSearch.Legacy.Bridge;
using Codex.ElasticSearch.Store;
using Codex.Import;
using Codex.Logging;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Utilities;
using Microsoft.Build.Locator;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

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
        static string loadDirectory;
        static List<string> binlogSearchPaths = new List<string>();
        static List<string> compilerArgumentsFiles = new List<string>();
        static string solutionPath;
        static string logDirectory = "logs";
        static bool interactive = false;
        static bool disableMsbuild = false;
        static bool disableMsbuildLocator = false;
        static ICodexStore store = Placeholder.Value<ICodexStore>("Create store (FileSystem | Elasticsearch)");
        static ElasticSearchService service;
        static bool reset = false;
        static bool clean = false;
        static bool newBackend = false;
        static bool scan = false;
        static bool test = false;
        static bool projectMode = false;
        static bool update = false;
        static List<string> deleteIndices = new List<string>();
        static List<string> demoteIndices = new List<string>();
        static List<string> promoteIndices = new List<string>();

        static Dictionary<string, (Action act, OptionSet options)> actions = new Dictionary<string, (Action, OptionSet)>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "index",
                (
                    new Action(() => Index()),
                    new OptionSet
                    {
                        { "noMsBuild", "Disable loading solutions using msbuild.", n => disableMsbuild = n != null },
                        { "noMsBuildLocator", "Disable loading solutions using msbuild.", n => disableMsbuildLocator = n != null },
                        { "es|elasticsearch=", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
                        { "save=", "Saves the analysis information to the given directory.", n => saveDirectory = n },
                        { "test", "Indicates that save should use test mode which disables optimization.", n => test = n != null },
                        { "clean", "Reset target index directory when using -save option.", n => clean = n != null },
                        { "n|name=", "Name of the repository.", n => repoName = StoreUtilities.GetSafeRepoName(n ?? string.Empty) },
                        { "p|path=", "Path to the repo to analyze.", n => rootDirectory = Path.GetFullPath(n) },
                        { "repoUrl=", "The URL of the repository being indexed", n => repoUrl = n },
                        { "bld|binLogSearchDirectory=", "Adds a bin log file or directory to search for binlog files", n => binlogSearchPaths.Add(n) },
                        { "ca|compilerArgumentFile=", "Adds a file specifying compiler arguments", n => compilerArgumentsFiles.Add(n) },
                        { "l|logDirectory=", "Optional. Path to log directory", n => logDirectory = n },
                        { "s|solution=", "Optionally, path to the solution to analyze.", n => solutionPath = n },
                        { "projectMode", "Uses project indexing mode.", n => projectMode = n != null },
                        { "newBackend", "Use new backend with stored filters Not supported.", n => newBackend = n != null },
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
                        { "l|logDirectory", "Optional. Path to log directory", n => logDirectory = n },
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
                        { "es|elasticsearch=", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
                        { "d=", "List the indices to delete.", n => deleteIndices.Add(n) },
                    }
                )
            },
            {
                "change",
                (
                    new Action(() => ChangeIndices()),
                    new OptionSet
                    {
                        { "es|elasticsearch=", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
                        { "promote=", "List the indices to promote.", n => promoteIndices.Add(n) },
                        { "demote=", "List the indices to demote.", n => demoteIndices.Add(n) },
                    }
                )
            },
            {
                "load",
                (
                    new Action(() => Load()),
                    new OptionSet
                    {
                        { "n|name=", "Name of the repository.", n => repoName = StoreUtilities.GetSafeRepoName(n ?? string.Empty) },
                        { "newBackend", "Use new backend with stored filters Not supported.", n => newBackend = n != null },
                        { "scan", "Treats every directory under data directory as a separate store to upload.", n => scan = n != null },
                        { "es|elasticsearch=", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
                        { "l|logDirectory", "Optional. Path to log directory", n => logDirectory = n },
                        { "reset", "Reset elasticsearch for indexing new set of data.", n => reset = n != null },
                        { "clean", "Reset target index directory when using -save option.", n => clean = n != null },
                        { "u", "Updates the analysis data (in place).", n => update = n != null },
                        { "d=", "The directory or a zip file containing analysis data to load.", n => loadDirectory = n },
                        { "save=", "Saves the analysis information to the given directory.", n => saveDirectory = n },
                        { "test", "Indicates that save should use test mode which disables optimization.", n => test = n != null },
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
                        { "es|elasticsearch=", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
                    }
                )
            }
        };

        static void Main(string[] args)
        {
            if (Environment.GetEnvironmentVariable("CodexDebugOnStart") == "1")
            {
                System.Diagnostics.Debugger.Launch();
            }

            try
            {
                AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                Console.WriteLine("Started");

                if (args.Length == 0)
                {
                    WriteHelpText();
                    return;
                }

                var remaining = args.Skip(1).ToArray();
                var verb = args[0].ToLowerInvariant();
                if (actions.TryGetValue(verb, out var action))
                {
                    var remainingArguments = action.options.Parse(remaining);
                    if (remainingArguments.Count != 0)
                    {
                        Console.Error.WriteLine($"Invalid argument(s): '{string.Join(", ", remainingArguments)}'");
                        WriteHelpText();
                        return;
                    }

                    Console.WriteLine("Parsed Arguments");
                    action.act();
                }
                else
                {
                    Console.Error.WriteLine($"Invalid verb '{verb}'");
                    WriteHelpText();
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }

        private static bool isReentrant = false;

        private static HashSet<string> knownMessages = new HashSet<string>()
        {
            "Unable to load DLL 'api-ms-win-core-file-l1-2-0.dll': The specified module could not be found. (Exception from HRESULT: 0x8007007E)",
            "Invalid cast from 'System.String' to 'System.Int32[]'.",
            "The given assembly name or codebase was invalid. (Exception from HRESULT: 0x80131047)",
            "Value was either too large or too small for a Decimal.",
        };

        private static void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            if (isReentrant)
            {
                return;
            }

            isReentrant = true;
            try
            {
                var ex = e.Exception;

                if (ex is InvalidCastException)
                {
                    if (ex.Message.Contains("Invalid cast from 'System.String' to"))
                    {
                        return;
                    }

                    if (ex.Message.Contains("Unable to cast object of type 'Microsoft.Build.Tasks.Windows.MarkupCompilePass1' to type 'Microsoft.Build.Framework.ITask'."))
                    {
                        return;
                    }
                }

                if (ex is InvalidOperationException)
                {
                    if (ex.Message.Contains("An attempt was made to transition a task to a final state when it had already completed."))
                    {
                        return;
                    }
                }

                if (ex is System.Net.WebException)
                {
                    if (ex.Message.Contains("(404) Not Found"))
                    {
                        return;
                    }
                }

                if (ex is AggregateException || ex is OperationCanceledException)
                {
                    return;
                }

                if (ex is DecoderFallbackException)
                {
                    return;
                }

                if (ex is DirectoryNotFoundException)
                {
                    return;
                }

                if (ex is Microsoft.Build.Exceptions.InvalidProjectFileException)
                {
                    return;
                }

                if (ex is FileNotFoundException)
                {
                    return;
                }

                if (ex is MissingMethodException)
                {
                    // MSBuild evaluation has a known one
                    return;
                }

                if (ex is XmlException && ex.Message.Contains("There are multiple root elements"))
                {
                    return;
                }

                if (knownMessages.Contains(ex.Message))
                {
                    return;
                }

                string exceptionType = ex.GetType().FullName;

                if (exceptionType.Contains("UnsupportedSignatureContent"))
                {
                    return;
                }

                string stackTrace = ex.StackTrace;
                if (stackTrace.Contains("at System.Guid.StringToInt"))
                {
                    return;
                }

                var message = DateTime.Now.ToString() + ": First chance exception: " + ex.ToString();

                Log(message);
            }
            finally
            {
                isReentrant = false;
            }
        }
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log(e.ExceptionObject?.ToString());
            try
            {
                Log(string.Join(Environment.NewLine, AppDomain.CurrentDomain.GetAssemblies().Select(a => $"{a.FullName ?? "Unknown Name"}: {a.Location ?? "Unknown Location"}")));
            }
            catch
            {
            }
        }

        private static void Log(string text)
        {
            Console.Error.WriteLine(text);
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
            if (!disableMsbuild && !disableMsbuildLocator)
            {
                try
                {
                    MSBuildLocator.RegisterDefaults();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error registering MSBuild locator: {ex.Message}");
                }
            }

            if (String.IsNullOrEmpty(rootDirectory)) throw new ArgumentException("Root path is missing. Use -p to provide it.");
            if (String.IsNullOrEmpty(repoName)) throw new ArgumentException("Project name is missing. Use -n to provide it.");
            if (saveDirectory == null)
            {
                if (newBackend)
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
                    store = new LegacyElasticSearchStore(new LegacyElasticSearchStoreConfiguration()
                    {
                        Endpoint = elasticSearchServer
                    });
                }
            }
            else
            {
                if (logDirectory == null)
                {
                    logDirectory = Path.Combine(saveDirectory, "logs");
                }

                store = new DirectoryCodexStore(saveDirectory) { DisableOptimization = test, Clean = clean };
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

        private static void ChangeIndices()
        {
            Storage.ElasticsearchStorage storage = new Storage.ElasticsearchStorage(elasticSearchServer);

            if ((promoteIndices.Count > 0) || (demoteIndices.Count > 0))
            {
                storage.Provider.ChangeIndices(promoteIndices: promoteIndices, demoteIndices: demoteIndices).GetAwaiter().GetResult();

                ListIndices();
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

        private static StreamWriter OpenLogWriter()
        {
            logDirectory = Path.GetFullPath(logDirectory);
            Directory.CreateDirectory(logDirectory);
            return new StreamWriter(Path.Combine(logDirectory, "cdx.log"));
        }

        private static void Load()
        {
            using (StreamWriter writer = OpenLogWriter())
            using (Logger logger = new MultiLogger(
                new ConsoleLogger(),
                new TextLogger(TextWriter.Synchronized(writer))))
            {
                if (scan)
                {
                    var clearIndicesBeforeUse = reset;
                    var directories = Directory.GetDirectories(loadDirectory);
                    int i = 1;
                    foreach (var directory in directories)
                    {
                        logger.LogMessage($"[{i} of {directories.Length}] Loading {directory}");
                        LoadCore(logger, directory, clearIndicesBeforeUse);

                        // Only clear indices on first use
                        clearIndicesBeforeUse = false;
                        i++;
                    }
                }
                else
                {
                    LoadCore(logger, loadDirectory, reset);
                }
            }
        }

        private static void LoadCore(Logger logger, string loadDirectory, bool clearIndicesBeforeUse)
        {
            if (File.Exists(Path.Combine(loadDirectory, @"store\repo.cdx.json")))
            {
                loadDirectory = Path.Combine(loadDirectory, "store");
            }

            Task.Run(async () =>
            {
                if (String.IsNullOrEmpty(loadDirectory)) throw new ArgumentException("Load directory must be specified. Use -d to provide it.");

                if (saveDirectory == null)
                {
                    if (newBackend)
                    {
                        InitService();
                        store = await service.CreateStoreAsync(new ElasticSearchStoreConfiguration()
                        {
                            Prefix = "test.",
                            ClearIndicesBeforeUse = reset
                        });
                    }
                    else
                    {
                        store = new LegacyElasticSearchStore(new LegacyElasticSearchStoreConfiguration()
                        {
                            Endpoint = elasticSearchServer
                        });
                    }
                }
                else
                {
                    store = new DirectoryCodexStore(saveDirectory) { Clean = clean, DisableOptimization = test };
                }

                var loadDirectoryStore = new DirectoryCodexStore(loadDirectory, logger);
                await loadDirectoryStore.ReadAsync(store, repositoryName: repoName);

            }).GetAwaiter().GetResult();
        }

        private static void ListIndices()
        {
            Storage.ElasticsearchStorage storage = new Storage.ElasticsearchStorage(elasticSearchServer);

            var indices = storage.Provider.GetIndicesAsync().GetAwaiter().GetResult();

            foreach (var index in indices)
            {
                if (index.IsActive)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                }

                Console.WriteLine(index.IndexName + (index.IsActive ? " (IsActive)" : ""));
            }

            Console.ForegroundColor = ConsoleColor.Gray;
        }

        private static void InitService()
        {
            if (String.IsNullOrEmpty(elasticSearchServer)) throw new ArgumentException("Elastic Search server URL is missing. Use -es to provide it.");

            service = new ElasticSearchService(new ElasticSearchServiceConfiguration(elasticSearchServer));
        }

        static async Task RunRepoImporter()
        {
            var targetIndexName = StoreUtilities.GetTargetIndexName(repoName);
            string[] file = new string[0];

            if (!string.IsNullOrEmpty(solutionPath))
            {
                solutionPath = Path.GetFullPath(solutionPath);
            }

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

            using (StreamWriter writer = OpenLogWriter())
            using (Logger logger = new MultiLogger(
                new ConsoleLogger(),
                new TextLogger(TextWriter.Synchronized(writer))))
            {
                FileSystem fileSystem = new CachingFileSystem(
                    new UnionFileSystem(file.Union(Enumerable.Repeat(solutionPath, String.IsNullOrEmpty(solutionPath) ? 0 : 1)),
                        new RootFileSystem(rootDirectory,
                            new MultiFileSystemFilter(
                                new DirectoryFileSystemFilter(@"\.", ".sln"),

                                new GitIgnoreFilter(),

                                // Filter out files from being indexed specified by the .cdxignore file
                                // This is used to ignore files which are not specified in the .gitignore files
                                new GitIgnoreFilter(".cdxignore"),

                                new BinaryFileSystemFilter(new string[] { ".exe", ".dll", "*.blob", ".db" })
                                ))
                        {
                            DisableEnumeration = file.Length != 0
                        }));

                var includedSolutions = !string.IsNullOrEmpty(solutionPath) ? new string[] { Path.GetFullPath(solutionPath) } : null;
                if (disableMsbuild)
                {
                    includedSolutions = new string[0];
                }

                List<RepoProjectAnalyzer> projectAnalyzers = new List<RepoProjectAnalyzer>()
                {
                    new CompilerArgumentsProjectAnalyzer(compilerArgumentsFiles.ToArray()),
                    new BinLogProjectAnalyzer(logger, binlogSearchPaths.ToArray())
                    {
                        RequireProjectFilesExist = requireProjectsExist
                    },
                    new MSBuildSolutionProjectAnalyzer(includedSolutions: includedSolutions)
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
