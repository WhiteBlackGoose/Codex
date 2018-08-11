using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Codex.ObjectModel;
using System;
using System.Threading;

namespace Codex.Utilities
{
    public static class ThreadingUtilities
    {
        public static T CompareSet<T>(ref T field, T value, T comparand) where T : class
        {
            var originalValue = Interlocked.CompareExchange(ref field, value, comparand);
            if (originalValue == comparand)
            {
                return value;
            }
            else
            {
                return originalValue;
            }
        }
    }
}
