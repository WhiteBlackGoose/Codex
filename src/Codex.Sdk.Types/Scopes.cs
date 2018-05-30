using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    /// <summary>
    /// Marker interface for searchable entities
    /// TODO: Consider moving <see cref="EntityContentId"/> out if its not needed by all searchable entities
    /// </summary>
    public partial interface ISearchEntity
    {
        string Uid { get; set; }

        /// <summary>
        /// Defines the content addressable identifier for the entity. This is used
        /// to determine if an entity with the same <see cref="Uid"/> should be updated
        /// </summary>
        [SearchBehavior(SearchBehavior.Term)]
        [Include(ObjectStage.Search)]
        string EntityContentId { get; set; }

        /// <summary>
        /// Defines the size of the raw serialized entity.
        /// </summary>
        [Include(ObjectStage.Search)]
        int EntityContentSize { get; set; }

        /// <summary>
        /// The version number used when storing the entity (for use by ElasticSearch concurrency control
        /// to prevent races when storing values)
        /// </summary>
        [SearchBehavior(SearchBehavior.None)]
        [Include(ObjectStage.Search)]
        long? EntityVersion { get; set; }

        /// <summary>
        /// The per-shard stable identity (derived from ElasticSearch sequence number)
        /// </summary>
        [SearchBehavior(SearchBehavior.Term)]
        [Include(ObjectStage.Search)]
        long ShardStableId { get; set; }

        /// <summary>
        /// Value used for sorting (this should be computed based other values in the entity i.e. {FileName}/{RepoRelativePath} for files)
        /// The goal is so that similar entities should be clustered together to allow maximum compression
        /// </summary>
        [SearchBehavior(SearchBehavior.Term)]
        [Include(ObjectStage.Search)]
        string SortKey { get; set; }

        ///// <summary>
        ///// Value used for routing (this should be computed based other values in the entity i.e. {FileName} for files)
        ///// The goal is so that similar entities should be routed to same shard to allow maximum compression
        ///// This should be composed into uid
        ///// </summary>
        //[SearchBehavior(SearchBehavior.Term)]
        //[Include(ObjectStage.None)]
        //string RoutingKey { get; set; }
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
