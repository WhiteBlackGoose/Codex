using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Codex.ObjectModel
{
    partial class BoundSourceFile
    {
        public void MakeSingleton()
        {
            Placeholder.NotImplemented("Singletons should use the content hash of the file as Uid. Consider two versions of the same MSBuild import at the same place on disk. Both should show up in the index.");
            Uid = SymbolId.CreateFromId($"{ProjectId}|{SourceFile.Info.ProjectRelativePath}").Value;
        }
    }
}