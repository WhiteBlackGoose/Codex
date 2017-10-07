using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    /// <summary>
    /// Class for marking places to fill in while implementing
    /// </summary>
    public static class Placeholder
    {
        public static T Value<T>(string message = null)
        {
            throw new NotImplementedException();
        }

        public static bool MissingFeature(string message = null)
        {
            return false;
        }

        public static Task NotImplementedAsync(string message = null)
        {
            throw new NotImplementedException();
        }

        public static Exception NotImplemented(string message = null)
        {
            throw new NotImplementedException();
        }
        
        [Conditional("TODO")]
        public static void Todo(string message = null)
        {
            throw new NotImplementedException();
        }
    }
}
