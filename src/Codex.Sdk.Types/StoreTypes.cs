using System;
using System.Collections.Generic;
using System.Text;

namespace Codex
{
    /// <summary>
    /// Information for creating an ICodexRepositoryStore
    /// </summary>
    public interface IRepositoryStoreInfo
    {
        /// <summary>
        /// The repository being stored
        /// </summary>
        IRepository Repository { get; }

        /// <summary>
        /// The branch being stored
        /// </summary>
        IBranch Branch { get; }

        /// <summary>
        /// The commit being stored
        /// </summary>
        ICommit Commit { get; }
    }

    /// <summary>
    /// Represents a directory in source control
    /// </summary>
    public interface ICommitFilesDirectory : IRepoFileScopeEntity
    {
        /// <summary>
        /// The files in the directory
        /// </summary>
        IReadOnlyList<ICommitFileLink> Files { get; }
    }

    // TODO: Need to persist full data for classifications and references for accurate replay
    public interface IStoredBoundSourceFile
    {
        IBoundSourceFile BoundSourceFile { get; }

        /// <summary>
        /// Compressed list of classification spans
        /// </summary>
        IClassificationList CompressedClassifications { get; }

        /// <summary>
        /// Compressed list of reference spans
        /// </summary>
        IReferenceList CompressedReferences { get; }
    }
}
