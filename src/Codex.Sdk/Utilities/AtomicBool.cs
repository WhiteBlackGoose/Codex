using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.Sdk.Utilities
{
    public class AtomicBool
    {
        private int m_value;

        private const int TRUE = 1;
        private const int FALSE = 0;

        public bool Value => Volatile.Read(ref m_value) == 1;

        public AtomicBool(bool initialValue = false)
        {
            m_value = initialValue ? TRUE : FALSE;
        }

        public bool TrySet(bool value)
        {
            var desiredValue = value ? TRUE : FALSE;
            var expectedValue = value ? FALSE : TRUE;

            if (Interlocked.CompareExchange(ref m_value, desiredValue, expectedValue) == expectedValue)
            {
                return true;
            }

            return false;
        }
    }
}
