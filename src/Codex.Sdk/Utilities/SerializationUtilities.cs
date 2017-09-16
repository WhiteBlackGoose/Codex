using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Utilities
{
    public static class SerializationUtilities
    {
        public static string AssignDuplicate(string value, ref string lastValue)
        {
            if (value == null)
            {
                return lastValue;
            }
            else
            {
                lastValue = value;
                return value;
            }
        }

        public static string RemoveDuplicate(string value, ref string lastValue)
        {
            if (value == lastValue)
            {
                return null;
            }
            else
            {
                lastValue = value;
                return value;
            }
        }
    }
}
