using Codex.Analysis;
using Codex.ElasticSearch;
using Codex.ElasticSearch.Legacy.Bridge;
using Codex.ElasticSearch.Store;
using Codex.Logging;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Utilities;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Codex.Storage
{
    public class CodexStorageApplication
    {
        protected string elasticSearchServer = GetEnvironmentVariableOrDefault("CodexUrl", "http://localhost:9200");
        protected bool finalize = true;
        protected string repoName;
        protected string alias;
        protected string saveDirectory;
        protected string loadDirectory;
        protected string logDirectory = "logs";
        protected ICodexStore store = Placeholder.Value<ICodexStore>("Create store (FileSystem | Elasticsearch)");
        protected ElasticSearchService service;
        protected bool reset = false;
        protected bool clean = false;
        protected bool newBackend = false;
        protected bool scan = false;
        protected bool test = false;
        protected string projectDataSuffix = "";
        protected bool update = false;
        protected List<string> deleteIndices = new List<string>();
        protected List<string> demoteIndices = new List<string>();
        protected List<string> promoteIndices = new List<string>();
        protected Dictionary<string, (Action act, OptionSet options)> actions;

        private static string GetEnvironmentVariableOrDefault(string name, string defaultValue = null)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        protected virtual IEnumerable<KeyValuePair<string, (Action, OptionSet)>> GetActions()
        {
            return new Dictionary<string, (Action, OptionSet)>(StringComparer.OrdinalIgnoreCase)
            {
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
                    "gc",
                    (
                        new Action(() => GarbageCollectIndices()),
                        new OptionSet
                        {
                            { "es|elasticsearch=", "URL of the ElasticSearch server.", n => elasticSearchServer = n },
                        }
                    )
                },
                {
                    "change",
                    (
                        new Action(() => ChangeIndices()),
                        new OptionSet
                        {
                            { "alias=", "The alias to modify.", n => alias = n },
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
        }

        public void Run(params string[] args)
        {
            ServicePointManager.Expect100Continue = true;

            try
            {
                AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                Console.WriteLine("Started");

                actions = GetActions().ToDictionarySafe(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase, overwrite: true);

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

        protected bool isReentrant = false;

        protected HashSet<string> knownMessages = new HashSet<string>()
        {
            "Unable to load DLL 'api-ms-win-core-file-l1-2-0.dll': The specified module could not be found. (Exception from HRESULT: 0x8007007E)",
            "Invalid cast from 'System.String' to 'System.Int32[]'.",
            "The given assembly name or codebase was invalid. (Exception from HRESULT: 0x80131047)",
            "Value was either too large or too small for a Decimal.",
        };

        protected virtual void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
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
                if (stackTrace?.Contains("at System.Guid.StringToInt") == true)
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
        protected void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
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

        protected void Log(string text)
        {
            Console.Error.WriteLine(text);
        }

        protected void WriteHelpText()
        {
            foreach (var actionEntry in actions)
            {
                Console.WriteLine($"codex {actionEntry.Key} {{options}}");
                Console.WriteLine("Options:");
                actionEntry.Value.options.WriteOptionDescriptions(Console.Out);
                Console.WriteLine();
            }
        }

        protected void Delete()
        {
            if (deleteIndices.Count != 0)
            {
                DeleteIndices();

                Console.WriteLine("Remaining Indices:");
                ListIndices();
                return;
            }
        }

        protected void ChangeIndices()
        {
            Storage.ElasticsearchStorage storage = new Storage.ElasticsearchStorage(elasticSearchServer);

            if ((promoteIndices.Count > 0) || (demoteIndices.Count > 0))
            {
                storage.Provider.ChangeIndices(promoteIndices: promoteIndices, demoteIndices: demoteIndices, alias: alias).GetAwaiter().GetResult();

                ListIndices();
            }
        }

        protected void GarbageCollectIndices()
        {
            InitService();
            Regex indexNameRegex = GetIndexNameRegex();
            var indices = GetIndices();
            var inactiveIndices = indices.Where(i => !i.IsActive).ToList();
            var inactiveIndexGroups = inactiveIndices
                .Where(i => indexNameRegex.IsMatch(i.IndexName))
                .GroupBy(i => indexNameRegex.Match(i.IndexName).Groups["name"].Value);

            foreach (var inactiveIndexGroup in inactiveIndexGroups)
            {
                var orderedIndices = inactiveIndexGroup
                    .OrderByDescending(i => DateTime.ParseExact(indexNameRegex.Match(i.IndexName).Groups["date"].Value, "yyMMdd.HHmmss", CultureInfo.InvariantCulture))
                    .ToList();

                var retainedIndex = orderedIndices[0];
                Console.WriteLine($"Garbage collecting: {inactiveIndexGroup.Key}");
                Console.WriteLine($"  Retaining {GetIndexDescription(retainedIndex)}");

                foreach (var index in orderedIndices.Skip(1))
                {
                    Console.Write($"  Deleting {GetIndexDescription(index)}... ");
                    var deleted = service.DeleteIndexAsync(index.IndexName).GetAwaiter().GetResult();
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
        }

        private static Regex GetIndexNameRegex()
        {
            return new Regex(@"^(?<name>.*)\.(?<date>\d{6}\.\d{6})$");
        }

        private string GetIndexDescription((string IndexName, bool IsActive, string Size) index)
        {
            return $"{index.IndexName} Size={index.Size}" + (index.IsActive ? " (IsActive)" : "");
        }

        protected void DeleteIndices()
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

        protected virtual Logger GetLogger()
        {
            return new MultiLogger(
                new ConsoleLogger(),
                new TextLogger(TextWriter.Synchronized(OpenLogWriter())));
        }

        protected StreamWriter OpenLogWriter()
        {
            logDirectory = Path.GetFullPath(logDirectory);
            Directory.CreateDirectory(logDirectory);
            return new StreamWriter(Path.Combine(logDirectory, "cdx.log"));
        }

        public void Ingest(
            string name,
            string elasticsearchUrl,
            string ingestionDirectory,
            bool finalize,
            bool useNewBackend)
        {
            repoName = name;
            newBackend = useNewBackend;
            loadDirectory = ingestionDirectory;
            elasticSearchServer = elasticsearchUrl;
            scan = true;
            Load(targetIndexName: StoreUtilities.GetTargetIndexName(name), finalize: finalize);
        }

        protected void Load(string targetIndexName = null, bool finalize = true)
        {
            using (var logger = GetLogger())
            {
                if (scan)
                {
                    var clearIndicesBeforeUse = reset;
                    var directories = Directory.GetFileSystemEntries(loadDirectory);
                    int i = 1;
                    foreach (var directory in directories)
                    {
                        var finalizeRepository =
                            (targetIndexName != null ? i == directories.Length : true) && finalize;
                        logger.LogMessage($"[{i} of {directories.Length}] Loading {directory}");
                        LoadCore(
                            logger,
                            directory,
                            clearIndicesBeforeUse,
                            finalizeRepository,
                            targetIndexName);

                        // Only clear indices on first use
                        clearIndicesBeforeUse = false;
                        i++;
                    }
                }
                else
                {
                    LoadCore(logger, loadDirectory, reset, finalizeRepository: finalize, targetIndexName: targetIndexName);
                }
            }
        }

        protected void LoadCore(
            Logger logger,
            string loadDirectory,
            bool clearIndicesBeforeUse,
            bool finalizeRepository,
            string targetIndexName = null)
        {
            if (File.Exists(Path.Combine(loadDirectory, @"store\repo.cdx.json")))
            {
                loadDirectory = Path.Combine(loadDirectory, "store");
            }

            LoadCoreAsync(logger, loadDirectory, finalizeRepository, targetIndexName).GetAwaiter().GetResult();
        }

        protected async Task LoadCoreAsync(Logger logger, string loadDirectory, bool finalizeRepository, string targetIndexName)
        {
            if (String.IsNullOrEmpty(loadDirectory)) throw new ArgumentException("Load directory must be specified. Use -d to provide it.");

            if (!string.IsNullOrEmpty(saveDirectory))
            {
                store = new DirectoryCodexStore(saveDirectory) { Clean = clean, DisableOptimization = test };
            }
            else if (!string.IsNullOrEmpty(elasticSearchServer))
            {
                if (newBackend)
                {
                    InitService();
                    store = await service.CreateStoreAsync(new ElasticSearchStoreConfiguration()
                    {
                        Prefix = "test.",
                        ClearIndicesBeforeUse = reset,
                        Logger = logger
                    });
                }
                else
                {
                    store = new LegacyElasticSearchStore(new LegacyElasticSearchStoreConfiguration()
                    {
                        Endpoint = elasticSearchServer,
                        TargetIndexName = targetIndexName
                    });
                }
            }
            else
            {
                store = new NullCodexRepositoryStore();
            }

            var loadDirectoryStore = new DirectoryCodexStore(loadDirectory, logger);
            await loadDirectoryStore.ReadAsync(store, repositoryName: repoName, finalize: finalizeRepository);
        }

        protected void ListIndices()
        {
            IEnumerable<(string IndexName, bool IsActive, string Size)> indices = GetIndices();
            var nameWidth = indices.Select(i => i.IndexName.Length).Max();
            var sizeWidth = indices.Select(i => i.Size.Length).Max();

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

                Console.WriteLine($"{index.IndexName.PadRight(nameWidth)} Size={index.Size.PadLeft(sizeWidth)}" + (index.IsActive ? " (IsActive)" : ""));
            }

            Console.ForegroundColor = ConsoleColor.Gray;
        }

        private IEnumerable<(string IndexName, bool IsActive, string Size)> GetIndices()
        {
            Storage.ElasticsearchStorage storage = new Storage.ElasticsearchStorage(elasticSearchServer);

            var indices = storage.Provider.GetIndicesAsync().GetAwaiter().GetResult();
            return indices;
        }

        protected void InitService()
        {
            if (String.IsNullOrEmpty(elasticSearchServer)) throw new ArgumentException("Elastic Search server URL is missing. Use -es to provide it.");

            service = new ElasticSearchService(new ElasticSearchServiceConfiguration(elasticSearchServer));
        }

        protected void Search()
        {
            SearchAsync().Wait();
        }

        protected async Task SearchAsync()
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
        }
    }
}
