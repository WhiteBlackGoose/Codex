using Codex.View;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Monaco;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Uno.Extensions;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Codex.Uno.Shared
{
    using static ViewBuilder;
    using static MainController;

    public class RightPaneView
    {
        public static FrameworkElement Create(RightPaneViewModel viewModel)
        {
            return new CodeEditor
            {
                Text = "Hello"
            };
        }
    }
}
