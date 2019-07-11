using Codex.Analysis;
using Codex.Analysis.External;
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
using Codex.Storage;
using Codex.Utilities;
using Microsoft.Build.Locator;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Codex.Application
{
    public class CodexApplication : CodexStorageApplication
    {
        protected bool analysisOnly = false; // Set this to disable uploading to ElasticSearch
        protected string rootDirectory;
        protected string repoUrl;
        protected List<string> binlogSearchPaths = new List<string>();
        protected List<string> compilerArgumentsFiles = new List<string>();
        protected string solutionPath;
        protected bool interactive = false;
        protected bool disableMsbuild = false;
        protected bool disableMsbuildLocator = false;
        protected bool disableEnumeration = false;
        protected bool projectMode = false;
        protected bool disableParallelFiles = false;
        protected bool detectGit = true;
        protected List<string> externalDataDirectories = new List<string>();
        protected List<string> projectDataDirectories = new List<string>();
        protected OptionSet indexOptions;

        protected override IEnumerable<KeyValuePair<string, (Action, OptionSet)>> GetActions()
        {
            return base.GetActions().Concat(
                new Dictionary<string, (Action, OptionSet)>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "index",
                        (
                            new Action(() => Index()),
                            indexOptions = new OptionSet
                            {
                                { "ed|extData=", "Specifies one or more external data directories.", n => externalDataDirectories.Add(n) },
                                { "pd|projectData=", "Specifies one or more project data directories.", n => projectDataDirectories.Add(n) },
                                { "pds|projectDataSuffix=", "Specifies the suffix for saving project data.", n => projectDataSuffix = n },
                                { "noScan", "Disable scanning enlistment directory.", n => disableEnumeration = n != null },
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
                                { "projectMode", "Uses project indexing mode.", n => detectGit = !(projectMode = n != null) },
                                { "disableParallelFiles", "Disables use of parallel file analysis.", n => disableParallelFiles = n == null },
                                { "disableDetectGit", "Disables use of LibGit2Sharp to detect git commit and branch.", n => detectGit = n == null },
                                { "newBackend", "Use new backend with stored filters Not supported.", n => newBackend = n != null },
                                { "i|interactive", "Search newly indexed items.", n => interactive = n != null }
                            }
                        )
                    },
                    {
                        "dryRun",
                        (
                            new Action(() => Analyze()),
                            indexOptions
                        )
                    }
                    });
        }

        protected override void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            var ex = e.Exception;

            if (ex is Microsoft.Build.Exceptions.InvalidProjectFileException)
            {
                return;
            }

            base.CurrentDomain_FirstChanceException(sender, e);
        }

        protected void Analyze()
        {
            analysisOnly = true;
            Index();
        }

        protected void Index()
        {
            if (!disableMsbuild && !disableMsbuildLocator)
            {
                MSBuildHelper.RegisterMSBuild();
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

                store = new DirectoryCodexStore(saveDirectory)
                {
                    DisableOptimization = test,
                    Clean = clean,
                    QualifierSuffix = projectDataSuffix
                };
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

        protected virtual IEnumerable<RepoProjectAnalyzer> GetProjectAnalyzers(AnalyzerArguments arguments)
        {
            var includedSolutions = !string.IsNullOrEmpty(solutionPath) ? new string[] { Path.GetFullPath(solutionPath) } : null;
            if (disableMsbuild)
            {
                includedSolutions = new string[0];
            }

            return new RepoProjectAnalyzer[]
            {
                new CompilerArgumentsProjectAnalyzer(compilerArgumentsFiles.ToArray()),
                new BinLogProjectAnalyzer(arguments.Logger, binlogSearchPaths.ToArray())
                {
                    RequireProjectFilesExist = arguments.RequireProjectFilesExist
                },
                new MSBuildSolutionProjectAnalyzer(includedSolutions: includedSolutions)
            };
        }

        protected class AnalyzerArguments
        {
            public Logger Logger { get; set; }
            public bool RequireProjectFilesExist { get; set; }
        }

        protected async Task RunRepoImporter()
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

            using (var logger = GetLogger())
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
                            DisableEnumeration = projectMode || disableEnumeration || file.Length != 0
                        }));

                List<RepoProjectAnalyzer> projectAnalyzers = new List<RepoProjectAnalyzer>(GetProjectAnalyzers(
                    new AnalyzerArguments()
                    {
                        Logger = logger,
                        RequireProjectFilesExist = requireProjectsExist
                    }));

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
                    var repoData = (
                        repository: new Repository(repoName)
                        {
                            SourceControlWebAddress = repoUrl,
                        },
                        commit: new Commit()
                        {
                            RepositoryName = repoName,
                            CommitId = targetIndexName,
                            DateUploaded = DateTime.UtcNow,
                        },
                        branch: new Branch()
                        {
                            HeadCommitId = targetIndexName,
                        });

                    if (detectGit)
                    {
                        GitHelpers.DetectGit(repoData, rootDirectory, logger);
                    }

                    analysisTarget = await store.CreateRepositoryStore(repoData.repository, repoData.commit, repoData.branch);
                }

                if (projectDataDirectories.Count != 0)
                {
                    var preAnalysisAnalyzer = new PreAnalyzedRepoProjectAnalyzer();

                    foreach (var projectDataDirectory in projectDataDirectories)
                    {
                        var interceptorStore = preAnalysisAnalyzer.CreateRepositoryStore(analysisTarget);
                        var directoryStore = new DirectoryCodexStore(projectDataDirectory, logger);

                        await directoryStore.ReadAsync(interceptorStore);
                    }

                    projectAnalyzers.Insert(0, preAnalysisAnalyzer);
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
                        // This indexer allows an external tool to write out codex spans for importing.
                        new ExternalRepoFileAnalyzer(externalDataDirectories.ToArray()),
                        new XmlFileAnalyzer(
                            ".ds",
                            ".xml",
                            ".config",
                            ".settings"),
                        })
                    {
                        RepositoryStore = analysisTarget,
                        Logger = logger,
                        ParallelProcessProjectFiles = projectMode && !disableParallelFiles
                    })
                {
                    AnalyzerDatas = projectAnalyzers.Select(a => new AnalyzerData() { Analyzer = a }).ToList()
                };

                await importer.Import(finalizeImport: finalize);
            }
        }
    }
}
