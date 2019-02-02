using Codex.Storage.Utilities;
using Codex.Utilities;

namespace Codex.ObjectModel
{
    partial class TextChunkSearchModel
    {
        private bool m_encoded = true;

        protected override void OnSerializingCore()
        {
            FullTextUtilities.EncodeFullText(Chunk?.ContentLines);
            base.OnSerializingCore();
        }

        protected override void OnDeserializedCore()
        {
            FullTextUtilities.DecodeFullText(Chunk?.ContentLines);
            base.OnDeserializedCore();
        }
    }
}