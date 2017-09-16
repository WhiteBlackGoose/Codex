using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    /// <summary>
    /// Marker interface for searchable entities
    /// TODO: Consider moving <see cref="ContentId"/> out if its not needed by all searchable entities
    /// </summary>
    public interface ISearchEntity
    {
        string Uid { get; }

        /// <summary>
        /// Defines the content addressable identifier for the entity. This is used
        /// to determine if an entity with the same <see cref="Uid"/> should be updated
        /// </summary>
        string ContentId { get; }
    }

    public interface IRepoScopeEntity
    {
        /// <summary>
        /// The name of the repository containing the entity
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string RepositoryName { get; }
    }

    public interface ICommitScopeEntity : IRepoScopeEntity
    {
        /// <summary>
        /// The unique identifier for this commit/changeset in version control
        /// (i.e. git commit hash or TFS changeset number)
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string CommitId { get; }
    }

    public interface IProjectScopeEntity : IRepoScopeEntity
    {
        /// <summary>
        /// The identifier of the project containing the entity
        /// </summary>
        [SearchBehavior(SearchBehavior.Sortword)]
        string ProjectId { get; }
    }

    public interface IRepoFileScopeEntity : IRepoScopeEntity
    {
        /// <summary>
        /// The repo relative path to the file
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string RepoRelativePath { get; }
    }

    public interface IProjectFileScopeEntity : IRepoFileScopeEntity, IProjectScopeEntity
    {
        /// <summary>
        /// The project relative path of the file
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string ProjectRelativePath { get; }
    }
}
