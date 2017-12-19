using Codex.Utilities;

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
            base.OnSerializingCore();
        }
    }
}