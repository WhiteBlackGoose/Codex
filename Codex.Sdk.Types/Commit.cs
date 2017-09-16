using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    /// <summary>
    /// Describes a commit in version control
    /// </summary>
    public interface ICommit : ICommitScopeEntity
    {
        /// <summary>
        /// The commit description describing the changes
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The date the commit was stored to the index
        /// </summary>
        [SearchBehavior(SearchBehavior.Sortword)]
        DateTime DateUploaded { get; }

        /// <summary>
        /// The date of the commit
        /// </summary>
        [SearchBehavior(SearchBehavior.Sortword)]
        DateTime DateCommitted { get; }

        /// <summary>
        /// The <see cref="ICommitScopeEntity.CommitId"/> of the parent commits
        /// </summary>
        IReadOnlyList<string> ParentCommitIds { get; }

        /// <summary>
        /// The files changed in the commit
        /// </summary>
        IReadOnlyList<ICommitChangedFile> ChangedFiles { get; }
    }

    /// <summary>
    /// Describes change kinds for files
    /// </summary>
    public enum FileChangeKind
    {
        Add,
        Edit,
        Rename,
        Delete
    }

    /// <summary>
    /// Represents a changed file in a commit
    /// </summary>
    public interface ICommitChangedFile : ICommitFileLink
    {
        /// <summary>
        /// The type of change applied to the file
        /// </summary>
        FileChangeKind ChangeKind { get; }

        /// <summary>
        /// For a renamed file, the path to the file prior to the rename
        /// </summary>
        string OriginalFilePath { get; }   
    }

    /// <summary>
    /// Represents a version of a repository file for a commit
    /// </summary>
    public interface ICommitFileLink
    {
        /// <summary>
        /// The relative path in the repository to the file
        /// </summary>
        string RepoRelativePath { get; }

        /// <summary>
        /// Unique identifer for file
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string FileId { get; }

        /// <summary>
        /// Unique identifer for file content as determined by version control 
        /// (i.e. the blob hash)
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string VersionControlFileId { get; }
    }

    /// <summary>
    /// Describes a branch in a repository
    /// </summary>
    public interface IBranch
    {
        /// <summary>
        /// The name of the branch
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The branch description
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The head commit of the branch
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string CommitId { get; }
    }
}
