using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Codex.ObjectModel
{
    partial class BoundSourceFile
    {
        public void MakeSingleton()
        {
            Uid = SymbolId.CreateFromId($"{ProjectId}|{SourceFile.Info.Path}").Value;
        }
    }
}