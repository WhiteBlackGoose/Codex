using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using System.Collections.Concurrent;
using Codex.Sdk.Utilities;
using System.IO;
using Codex.Utilities;
using Codex.Serialization;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Codex.Storage.DataModel;

namespace Codex.ElasticSearch.Store
{
    public class DirectoryCodexStore : ICodexStore, ICodexRepositoryStore
    {
        private readonly ConcurrentQueue<ValueTask<None>> backgroundTasks = new ConcurrentQueue<ValueTask<None>>();
        private readonly ConcurrentQueue<CommitFileLink> commitFiles = new ConcurrentQueue<CommitFileLink>();

        private readonly string m_directory;

        private const string EntityFileExtension = ".cdx.json";

        // These two files should contain the same content after finalization
        private const string RepositoryInfoFileName = "repo" + EntityFileExtension;
        private const string RepositoryInitializationFileName = "initrepo" + EntityFileExtension;

        private RepositoryStoreInfo m_storeInfo;

        public DirectoryCodexStore(string directory)
        {
            m_directory = directory;
        }

        public async Task Read(ICodexStore store)
        {
            m_storeInfo = Read<RepositoryStoreInfo>(RepositoryInitializationFileName);
            var repositoryStore = await store.CreateRepositoryStore(m_storeInfo.Repository, m_storeInfo.Commit, m_storeInfo.Branch);

            List<Task> tasks = new List<Task>();
            foreach (var kind in StoredEntityKind.Kinds)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var kindDirectoryPath = Path.Combine(m_directory, kind.Name);
                    if (Directory.Exists(kindDirectoryPath))
                    {
                        foreach (var file in Directory.EnumerateFiles(kindDirectoryPath))
                        {
                            await kind.Add(this, file, repositoryStore);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }

        public Task<ICodexRepositoryStore> CreateRepositoryStore(Repository repository, Commit commit, Branch branch)
        {
            var allFiles = Directory.GetFiles(m_directory, "*.*", SearchOption.AllDirectories);
            foreach (var file in allFiles)
            {
                if (file.EndsWith(EntityFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                }
            }

            lock (this)
            {
                Contract.Assert(m_storeInfo == null);
                m_storeInfo = new RepositoryStoreInfo()
                {
                    Repository = repository,
                    Commit = commit,
                    Branch = branch
                };
            }
            Write(RepositoryInitializationFileName, m_storeInfo);

            return Task.FromResult<ICodexRepositoryStore>(this);
        }

        private Task AddAsync<T>(IReadOnlyList<T> entities, StoredEntityKind<T> kind, Func<T, string> pathGenerator)
            where T : EntityBase
        {
            foreach (var entity in entities)
            {
                var contentId = entity.GetEntityContentId();
                var pathPart = pathGenerator(entity);

                Write(Path.Combine(kind.Name, pathPart + contentId + EntityFileExtension), entity);
            }

            return Task.CompletedTask;
        }

        private void Write<T>(string relativePath, T entity)
        {
            var fullPath = Path.Combine(m_directory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (var streamWriter = new StreamWriter(fullPath))
            {
                entity.SerializeEntityTo(new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented });
            }
        }

        private T Read<T>(string relativePath)
        {
            var fullPath = Path.Combine(m_directory, relativePath);
            using (var streamReader = new StreamReader(fullPath))
            {
                return streamReader.DeserializeEntity<T>();
            }
        }

        #region ICodexRepositoryStore Members

        public Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
        {
            return AddAsync(files.SelectList(CreateStoredBoundFile), StoredEntityKind.BoundFiles, e => Path.Combine(e.BoundSourceFile.ProjectId, Path.GetFileName(e.BoundSourceFile.SourceFile.Info.RepoRelativePath)));
        }

        private StoredBoundSourceFile CreateStoredBoundFile(BoundSourceFile boundSourceFile)
        {
            var result = new StoredBoundSourceFile()
            {
                BoundSourceFile = boundSourceFile,
                CompressedClassifications = new ClassificationListModel(boundSourceFile.Classifications),
                CompressedReferences = new ReferenceListModel(boundSourceFile.References, includeLineInfo: true),
            };

            boundSourceFile.References = CollectionUtilities.Empty<ReferenceSpan>.Array;
            boundSourceFile.Classifications = CollectionUtilities.Empty<ClassificationSpan>.Array;

            return result;
        }

        private BoundSourceFile FromStoredBoundFile(StoredBoundSourceFile storedBoundFile)
        {
            var boundSourceFile = storedBoundFile.BoundSourceFile;

            boundSourceFile.Classifications = storedBoundFile.CompressedClassifications.ToList();
            boundSourceFile.References = storedBoundFile.CompressedReferences.ToList();

            return boundSourceFile;
        }

        public Task AddCommitFilesAsync(IReadOnlyList<CommitFileLink> links)
        {
            foreach (var link in links)
            {
                commitFiles.Enqueue(link);
            }

            return Task.CompletedTask;
        }

        public Task AddLanguagesAsync(IReadOnlyList<LanguageInfo> languages)
        {
            return Placeholder.NotImplementedAsync();
        }

        public Task AddProjectsAsync(IReadOnlyList<AnalyzedProject> projects)
        {
            return AddAsync(projects, StoredEntityKind.Projects, e => Path.GetFileName(e.ProjectId));
        }

        public Task AddTextFilesAsync(IReadOnlyList<SourceFile> files)
        {
            return AddAsync(files, StoredEntityKind.TextFiles, e => Path.GetFileName(e.Info.RepoRelativePath));
        }

        public async Task FinalizeAsync()
        {
            await AddAsync(commitFiles.GroupBy(f => Path.GetDirectoryName(f.RepoRelativePath), StringComparer.OrdinalIgnoreCase)
                .Select(g => new CommitFilesDirectory()
                {
                    RepoRelativePath = g.Key,
                    Files = g.OrderBy(c => c.RepoRelativePath, StringComparer.OrdinalIgnoreCase).ToList()
                }).ToList(), StoredEntityKind.CommitDirectories, e => Path.GetFileName(e.RepoRelativePath) ?? string.Empty);

            // Flush any background operations
            while (backgroundTasks.TryDequeue(out var backgroundTask))
            {
                await backgroundTask;
            }

            Write(RepositoryInfoFileName, m_storeInfo);
        }

        #endregion ICodexRepositoryStore Members

        private abstract class StoredEntityKind
        {
            public static readonly StoredEntityKind<StoredBoundSourceFile> BoundFiles = Create<StoredBoundSourceFile>((entity, repositoryStore) => repositoryStore.AddBoundFilesAsync(new[] { entity.BoundSourceFile }));
            public static readonly StoredEntityKind<AnalyzedProject> Projects = Create<AnalyzedProject>((entity, repositoryStore) => repositoryStore.AddProjectsAsync(new[] { entity }));
            public static readonly StoredEntityKind<SourceFile> TextFiles = Create<SourceFile>((entity, repositoryStore) => repositoryStore.AddTextFilesAsync(new[] { entity }));
            public static readonly StoredEntityKind<CommitFilesDirectory> CommitDirectories = Create<CommitFilesDirectory>((entity, repositoryStore) => repositoryStore.AddCommitFilesAsync(entity.Files));

            public static IReadOnlyList<StoredEntityKind> Kinds => Inner.Kinds;

            public static StoredEntityKind<T> Create<T>(Func<T, ICodexRepositoryStore, Task> add, [CallerMemberName] string name = null)
            {
                var kind = new StoredEntityKind<T>(add, name);
                Inner.Kinds.Add(kind);
                return kind;
            }

            public abstract string Name { get; }

            public abstract Task Add(DirectoryCodexStore store, string fullPath, ICodexRepositoryStore repositoryStore);

            private static class Inner
            {
                public static readonly List<StoredEntityKind> Kinds = new List<StoredEntityKind>();
            }
        }

        private class StoredEntityKind<T> : StoredEntityKind
        {
            public override string Name { get; }

            private Func<T, ICodexRepositoryStore, Task> add;

            public StoredEntityKind(Func<T, ICodexRepositoryStore, Task> add, string name)
            {
                Name = name;
                this.add = add;
            }

            public override Task Add(DirectoryCodexStore store, string fullPath, ICodexRepositoryStore repositoryStore)
            {
                var entity = store.Read<T>(fullPath);
                return add(entity, repositoryStore);
            }
        }
    }
}
