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

    [RequiredFor(ObjectStage.Upload)]
    public interface IRepoScopeEntity
    {
        string RepositoryName { get; }
    }

    [RequiredFor(ObjectStage.Upload)]
    public interface ICommitScopeEntity
    {
        [Restricted(ObjectStage.Upload)]
        string CommitId { get; }
    }

    [RequiredFor(ObjectStage.Upload)]
    public interface IProjectScopeEntity : IRepoScopeEntity
    {
        [SearchBehavior(SearchBehavior.Sortword)]
        string ProjectId { get; }
    }

    [RequiredFor(ObjectStage.Upload)]
    public interface IFileScopeEntity : IProjectScopeEntity
    {
        /// <summary>
        /// The project relative path of the file
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string FilePath { get; }

        /// <summary>
        /// The unique identifier for the file
        /// </summary>
        string FileId { get; }
    }
}
