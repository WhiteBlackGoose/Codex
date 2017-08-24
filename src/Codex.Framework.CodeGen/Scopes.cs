using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
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

        [Restricted(ObjectStage.Upload)]
        int StableId { get; }
    }

    [RequiredFor(ObjectStage.Upload)]
    public interface IProjectScopeEntity : IRepoScopeEntity
    {
        string ProjectId { get; }
    }

    [RequiredFor(ObjectStage.Upload)]
    public interface IFileScopeEntity : IProjectScopeEntity
    {
        /// <summary>
        /// The language of the file
        /// </summary>
        string Language { get; }

        /// <summary>
        /// The project relative path of the file
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// The repo relative path of the file
        /// </summary>
        string RepoRelativePath { get; }

        /// <summary>
        /// The unique identifier for the file
        /// </summary>
        string FileId { get; }
    }
}
