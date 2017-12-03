using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    public static class CompatibilityExtensions
    {
        public static string ToLowerInvariant(this string value)
        {
            return value.ToLower();
        }
    }
}
