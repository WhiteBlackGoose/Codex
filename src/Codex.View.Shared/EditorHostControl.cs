using System.Windows;

namespace Codex.View
{
    public partial class EditorHostControl : FrameworkElement
    {
        public EditorHostControl()
        {
            Focusable = true;
            IsHitTestVisible = true;
        }
    }
}
