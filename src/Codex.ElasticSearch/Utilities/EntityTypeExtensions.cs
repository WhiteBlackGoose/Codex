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

            // TODO: These properties should not be defined on ISourceFileInfo as they require binding information
            boundSourceFile.Language = boundSourceFile.Language ?? sourceFileInfo.Language;

            // Apply the final versions of the properties to the source file info
            sourceFileInfo.Language = boundSourceFile.Language;

            return boundSourceFile;
        }

    }
}
