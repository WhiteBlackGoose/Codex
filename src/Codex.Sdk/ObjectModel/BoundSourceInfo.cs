using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Codex.ObjectModel
{
    partial class BoundSourceInfo
    {
        public int CoerceReferenceCount(int? value)
        {
            return value ?? References.Count;
        }

        public int CoerceDefinitionCount(int? value)
        {
            return value ?? Definitions.Count;
        }
    }
}