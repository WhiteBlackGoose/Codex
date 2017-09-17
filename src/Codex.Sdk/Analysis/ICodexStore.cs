using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Utilities;
using Codex.Framework.Types;

namespace Codex.Analysis
{
    public interface ICodexStore
    {
        // TODO: NOTE: Need to watch out for this deleting stored filters
        /// <summary>
        /// Creates a new <see cref="ICodexRepositoryStore"/> over the given repository and commit.
        /// Entities added to the store will be accumulated with the stored filter for the commit/repo.
        /// </summary>
        Task<ICodexRepositoryStore> CreateRepositoryStore(IRepository repository, ICommit commit, IBranch branch);
    }

    public interface IAdministratorCodexStore
    {
        /// <summary>
        /// Updates the portal to view the given commit of the repository
        /// </summary>
        /// <param name="portalName">the name of the portal view</param>
        /// <param name="repositoryName">the name of the repository</param>
        /// <param name="commitId">the commit id</param>
        /// <param name="branchName">the name of the branch referencing the commit</param>
        Task UpdatePortalAsync(string portalName, string repositoryName, string commitId, string branchName);
    }

    public interface ICodexRepositoryStore
    {
        /// <summary>
        /// Adds source files with only raw text information
        /// Affected search stores:
        /// <see cref="SearchTypes.TextSource"/>
        /// <see cref="SearchTypes.Property"/> ?
        /// <see cref="SearchTypes.CommitFiles"/>
        /// </summary>
        Task AddTextFilesAsync(IReadOnlyList<SourceFile> files);

        /// <summary>
        /// Adds source files with semantic binding information.
        /// Affected search stores:
        /// <see cref="SearchTypes.BoundSource"/>
        /// <see cref="SearchTypes.TextSource"/>
        /// <see cref="SearchTypes.Definition"/>
        /// <see cref="SearchTypes.Reference"/>
        /// <see cref="SearchTypes.Property"/>
        /// <see cref="SearchTypes.CommitFiles"/>
        /// </summary>
        Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files);

        /// <summary>
        /// Adds repository projects
        /// Affected search stores:
        /// <see cref="SearchTypes.Project"/>
        /// <see cref="SearchTypes.ProjectReference"/>
        /// <see cref="SearchTypes.Property"/> ?
        /// May also call <see cref="AddBoundFilesAsync(IReadOnlyList{IBoundSourceFile})"/> for additional source files
        /// </summary>
        Task AddProjectsAsync(IReadOnlyList<AnalyzedProject> files);

        /// <summary>
        /// Adds language information
        /// Affected search stores:
        /// <see cref="SearchTypes.Language"/>
        /// </summary>
        Task AddLanguagesAsync(IReadOnlyList<LanguageInfo> files);

        /// <summary>
        /// Explicityly adds commit files. NOTE: This is not generally necessary since <see cref="AddBoundFilesAsync(IReadOnlyList{IBoundSourceFile})"/>
        /// and <see cref="AddTextFilesAsync(IReadOnlyList{ISourceFile})"/> implicitly add commit files.
        /// Affected search stores:
        /// <see cref="SearchTypes.CommitFiles"/>
        /// </summary>
        Task AddCommitFilesAsync(IReadOnlyList<CommitFileLink> files);

        /// <summary>
        /// Finalizes the store and flushes any outstanding operations
        /// </summary>
        Task FinalizeAsync();
    }
}
