using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;

namespace Codex.Utilities
{
    public static partial class CollectionUtilities
    {
        public class Empty<T>
        {
            public static readonly List<T> List = new List<T>(0);

            public static readonly T[] Array = new T[0];
        }
    }
}
