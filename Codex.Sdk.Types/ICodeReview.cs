using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    // TODO: These should be search types
    // Search Type
    [Migrated]
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
        /// <summary>
        /// The first iteration in which this file appears
        /// </summary>
        int StartIteration { get; }

        /// <summary>
        /// The relative path in the repository
        /// </summary>
        string RepoRelativePath { get; }

        // TODO: This should be a reference to the sources index
        /// <summary>
        /// The file id of the new version of the file
        /// </summary>
        string FileId { get; }

        // TODO: This should be a reference to the sources index
        /// <summary>
        /// The file id of the baseline version of the file
        /// </summary>
        string BaselineFileId { get; }

        /// <summary>
        /// The type of change applied to the file
        /// </summary>
        FileChangeKind ChangeKind { get; }
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
        /// <summary>
        /// The original location for the comment in the originating iteration
        /// </summary>
        ILineSpan OriginalSpan { get; }

        /// <summary>
        /// The iteration where the comment originated
        /// </summary>
        int StartIteration { get; }

        /// <summary>
        /// The last tie
        /// </summary>
        DateTime LastUpdated { get; }

        string FileRepoRelativePath { get; }

        IReadOnlyList<ICodeReviewComment> Comments { get; }
    }

    public interface ICodeReviewComment
    {
        string Text { get; }

        /// <summary>
        /// The name of the reviewer which made the comment
        /// </summary>
        string Reviewer { get; }

        /// <summary>
        /// The importance of the comment
        /// </summary>
        CommentImportance Importance { get; }

        /// <summary>
        /// The time when the comment was submitted
        /// </summary>
        DateTime CommentTime { get; }
    }
}
