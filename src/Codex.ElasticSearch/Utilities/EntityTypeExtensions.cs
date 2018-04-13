using Codex.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Utilities
{
    public static class EntityTypeExtensions
    {
        public static BoundSourceFile ApplySourceFileInfo(this BoundSourceFile boundSourceFile)
        {
            var sourceFileInfo = boundSourceFile.SourceFile.Info;

            boundSourceFile.RepositoryName = boundSourceFile.RepositoryName ?? sourceFileInfo.RepositoryName;
            sourceFileInfo.RepositoryName = boundSourceFile.RepositoryName;

            boundSourceFile.RepoRelativePath = boundSourceFile.RepoRelativePath ?? sourceFileInfo.RepoRelativePath;
            sourceFileInfo.RepoRelativePath = boundSourceFile.RepoRelativePath;

            // TODO: These properties should not be defined on ISourceFileInfo as they require binding information
            boundSourceFile.Language = boundSourceFile.Language ?? sourceFileInfo.Language;
            boundSourceFile.ProjectRelativePath = boundSourceFile.ProjectRelativePath ?? sourceFileInfo.ProjectRelativePath;
            boundSourceFile.ProjectId = boundSourceFile.ProjectId ?? sourceFileInfo.ProjectId;

            // Apply the final versions of the properties to the source file info
            sourceFileInfo.Language = boundSourceFile.Language;
            sourceFileInfo.ProjectRelativePath = boundSourceFile.ProjectRelativePath;
            sourceFileInfo.ProjectId = boundSourceFile.ProjectId;

            return boundSourceFile;
        }

    }
}
