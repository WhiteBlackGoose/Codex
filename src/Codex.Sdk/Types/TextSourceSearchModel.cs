using Codex.Storage.Utilities;
using Codex.Utilities;

namespace Codex.ObjectModel
{
    partial class TextSourceSearchModel
    {
        private bool m_encoded = true;

        protected override void OnDeserializedCore()
        {
            if(m_encoded && File != null)
            {
                File.Content = FullTextUtilities.DecodeFullTextString(File.Content);
                m_encoded = false;
            }

            base.OnDeserializedCore();
        }
    }
}