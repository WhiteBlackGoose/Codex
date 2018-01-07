using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Utilities
{
    public static class SerializationUtilities
    {
        public static T AssignDuplicate<T>(T value, ref T lastValue)
        {
            if (EqualityComparer<T>.Default.Equals(value, default(T)))
            {
                return lastValue;
            }
            else
            {
                lastValue = value;
                return value;
            }
        }

        public static T RemoveDuplicate<T>(T value, ref T lastValue)
        {
            if (EqualityComparer<T>.Default.Equals(value, lastValue))
            {
                return default(T);
            }
            else
            {
                lastValue = value;
                return value;
            }
        }
    }
}
