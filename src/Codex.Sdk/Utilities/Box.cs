using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Sdk.Utilities
{
    public class Box<T>
    {
        public T Value;

        public Box()
        {
        }

        public Box(T value)
        {
            Value = value;
        }

        public static implicit operator Box<T>(T value)
        {
            return new Box<T>(value);
        }
    }
}
