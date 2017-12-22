using System.Windows;
using System.Windows.Controls;

namespace Codex.View
{
#if !BRIDGE
    public partial class LeftPaneContentView : ContentControl
    {
        public void RenderContent(LeftPaneView view, LeftPaneContent viewModel)
        {
        }
    }
#endif
}
