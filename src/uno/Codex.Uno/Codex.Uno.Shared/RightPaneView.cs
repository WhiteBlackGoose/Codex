using Codex.View;
using Codex.Web.Mvc.Rendering;
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
            var content = viewModel.SourceFile == null ? "Empty" : new SourceFileRenderer(viewModel.SourceFile).RenderHtml();

            return new Grid()
            {
                RowDefinitions =
                {
                    new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition() { Height = GridLength.Auto }
                },
            }.WithChildren(
                Row(0, new WasmHtmlContentControl
                {
                    HtmlContent = content
                }),
                Row(1, CreateEditor(viewModel)),
                Row(2, new Border()
                {
                    Height = 56,
                    Background = B(Colors.Purple)
                })
            );
        }

        private static CodeEditor CreateEditor(RightPaneViewModel viewModel)
        {
            var editor = new CodeEditor();
            editor.Loading += (s, e) =>
            {
                editor.Text = viewModel.SourceFile?.SourceFile.Content ?? "Test me";
            };

            return editor;
        }
    }
}
