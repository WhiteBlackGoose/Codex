using Codex.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Codex.ObjectModel
{
    partial class BoundSourceInfo
    {
        public int CoerceReferenceCount(int? value)
        {
            return value ?? References.Count;
        }

        public int CoerceDefinitionCount(int? value)
        {
            return value ?? Definitions.Count;
        }
    }

    partial class BoundSourceSearchModel
    {
        protected override void OnDeserializedCore()
        {
            if (CompressedClassifications != null)
            {
                BindingInfo.Classifications = CompressedClassifications.ToList();
            }

            if (CompressedReferences != null)
            {
                BindingInfo.References = CompressedReferences.ToList();
            }

            base.OnDeserializedCore();
        }

        protected override void OnSerializingCore()
        {
            if (CompressedClassifications != null)
            {
                BindingInfo.Classifications = CollectionUtilities.Empty<ClassificationSpan>.Array;
            }

            if (CompressedReferences != null)
            {
                BindingInfo.References = CollectionUtilities.Empty<ReferenceSpan>.Array;
            }

            base.OnSerializingCore();
        }
    }
}