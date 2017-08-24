using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    // TODO: These should be search types
    // Search Type
    public interface ICodeReview
    {
        string Id { get; }

        string Description { get; }

        /// <summary>
        /// Url for accessing code review in source control management (i.e. GitHub or VSO)
        /// </summary>
        string Url { get; }

        CodeReviewStatus Status { get; }
    }

    public enum CodeReviewStatus
    {

    }

    // Search Type
    public interface ICodeReviewIteration
    {
        int IterationNumber { get; }

        string ReviewId { get; }

        string Description { get; }

        IReadOnlyList<ICodeReviewFile> Files { get; }
    }

    public enum CodeReviewerStatus
    {
        NotStarted,
        Waiting,
        ApprovedWithSuggestions,
        Approved,
        Declined
    }


    public interface ICodeReviewerInfo
    {
        string Name { get; }
    }

    public interface ICodeReviewFile
    {
        // ?
        int StartIteration { get; }

        string RepoRelativePath { get; }

        // TODO: This should be a reference to the sources index
        string FileId { get; }

        // TODO: This should be a reference to the sources index
        string BaselineFileId { get; }

        // TODO: Should this be enum?
        string ChangeKind { get; }
    }

    public enum CodeReviewFileChangeKind
    {
        Edit,
        Add,
        Delete,

        /// <summary>
        /// Move or rename
        /// </summary>
        Rename,

    }

    public enum CommentImportance
    {
        /// <summary>
        /// Indicates that the author can decide to take the change or not
        /// </summary>
        AuthorDecides,

        /// <summary>
        /// Default importance
        /// </summary>
        Info,

        /// <summary>
        /// Reviewer would like further discussion on this comment
        /// </summary>
        Discuss,

        /// <summary>
        /// Waiting on this comment to be addressed in order to approve
        /// </summary>
        Blocker,
    }

    public enum CommentStatus
    {
        Unpublished,
        Active,
        Resolved,
        WontFix,
        Pending,
        Closed
    }

    // Search Type
    public interface ICodeReviewCommentThread
    {
        ILineSpan OriginalSpan { get; }

        int StartIteration { get; }

        DateTime LastUpdated { get; }

        string FileRepoRelativePath { get; }

        IReadOnlyList<ICodeReviewComment> Comments { get; }
    }

    public interface ICodeReviewComment
    {
        string Text { get; }

        string Reviewer { get; }

        CommentImportance Importance { get; }
    }
}
