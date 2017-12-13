using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public static class EntityUtilities
    {
        public static TTarget NullOrCopy<T, TTarget>(T obj, Func<T, TTarget> copy)
            where T : class
            where TTarget : class, T
        {
            if (obj == null) return null;
            return copy(obj);
        }
    }
}