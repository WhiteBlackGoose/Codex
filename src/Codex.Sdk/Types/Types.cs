using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.ObjectModel
{
    public abstract partial class CodexTypeUtilities
    {
        public Type GetInterfaceType(Type type)
        {
            if (!type.IsInterface)
            {
                TryGetMappedType(type, out var result);
                return result ?? type;
            }

            return type;
        }

        public Type GetImplementationType(Type type)
        {
            if (type.IsInterface)
            {
                TryGetMappedType(type, out var result);
                return result ?? type;
            }

            return type;
        }

        public bool IsEntityType(Type type)
        {
            return TryGetMappedType(type, out var mappedType);
        }

        protected virtual bool TryGetMappedType(Type type, out Type mappedType)
        {
            return s_typeMappings.TryGetValue(type, out mappedType);
        }
    }

    public partial class PropertyMapBase
    {
        public PropertyMapBase() : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }

    partial class DefinitionSymbol
    {
        public int ReferenceCount;

        public void IncrementReferenceCount()
        {
            Interlocked.Increment(ref ReferenceCount);
        }
    }
}
