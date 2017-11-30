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

namespace Codex.ElasticSearch.Store
{
    public class DirectoryCodexStore : ICodexStore, ICodexRepositoryStore
    {
        private readonly ConcurrentQueue<ValueTask<None>> backgroundTasks = new ConcurrentQueue<ValueTask<None>>();
        private readonly ConcurrentQueue<CommitFileLink> commitFiles = new ConcurrentQueue<CommitFileLink>();

        private readonly string m_directory;

        // These two files should contain the same content after finalization
        private const string RepositoryInfoFileName = "repo.json";
        private const string RepositoryInitializationFileName = "initrepo.json";

        private RepositoryStoreInfo m_storeInfo;

        public DirectoryCodexStore(string directory)
        {
            m_directory = directory;
        }

        public Task<ICodexRepositoryStore> CreateRepositoryStore(Repository repository, Commit commit, Branch branch)
        {
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

        public Task AddAsync<T>(IReadOnlyList<T> entities, string kind, Func<T, string> pathGenerator)
            where T : EntityBase
        {
            foreach (var entity in entities)
            {
                var contentId = entity.GetEntityContentId();
                var pathPart = pathGenerator(entity);

                Write(Path.Combine(kind, pathPart + contentId + ".json"), entity);
            }

            return Task.CompletedTask;
        }

        private void Write<T>(string relativePath, T entity)
        {
            var fullPath = Path.Combine(m_directory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (var streamWriter = new StreamWriter(fullPath))
            {
                entity.SerializeEntityTo(streamWriter);
            }
        }

        #region ICodexRepositoryStore Members

        public Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
        {
            return AddAsync(files, "BoundFiles", e => Path.Combine(e.ProjectId, Path.GetFileName(e.SourceFile.Info.RepoRelativePath)));
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
            return AddAsync(projects, "Projects", e => Path.GetFileName(e.ProjectId));
        }

        public Task AddTextFilesAsync(IReadOnlyList<SourceFile> files)
        {
            return AddAsync(files, "TextFiles", e => Path.GetFileName(e.Info.RepoRelativePath));
        }

        public async Task FinalizeAsync()
        {
            await AddAsync(commitFiles.GroupBy(f => Path.GetDirectoryName(f.RepoRelativePath), StringComparer.OrdinalIgnoreCase)
                .Select(g => new CommitFilesDirectory()
                {
                    RepoRelativePath = g.Key,
                    Files = g.OrderBy(c => c.RepoRelativePath, StringComparer.OrdinalIgnoreCase).ToList()
                }).ToList(), "CommitDirectories", e => Path.GetFileName(e.RepoRelativePath) ?? string.Empty);

            // Flush any background operations
            while (backgroundTasks.TryDequeue(out var backgroundTask))
            {
                await backgroundTask;
            }

            Write(RepositoryInfoFileName, m_storeInfo);
        }

        #endregion ICodexRepositoryStore Members
    }
}
