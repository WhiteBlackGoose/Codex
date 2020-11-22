
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml;

namespace Codex.Uno.Shared
{
    [ContentProperty(Name = nameof(HtmlContent))]
    public class WasmHtmlContentControl : Control
    {
        public WasmHtmlContentControl()
        {
        }

        private string _html;

        public string HtmlContent
        {
            get => _html;
            set
            {
#if __WASM__
                this.SetHtmlContent(value);
#endif
                _html = value;
            }
        }
    }
}