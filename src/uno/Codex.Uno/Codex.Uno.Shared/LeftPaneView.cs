using Codex.View;
using Microsoft.Toolkit.Uwp.UI.Controls;
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

    public class LeftPaneView
    {
        public static FrameworkElement Create(LeftPaneViewModel viewModel)
        {
            return new DockPanel().WithChildren(
                Top(
                    new Border()
                    {
                        Height = 38,
                        Background = B(C(0xFFFFE0)),
                        BorderBrush = B(C(0xF0E68C)),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(8),
                        VerticalAlignment = VerticalAlignment.Top,
                        Padding = new Thickness(8, 0, 8, 0),
                    }
                    .HideIfNull(viewModel.SearchInfoBinding)
                    .WithChild(
                        new TextBlock()
                        {
                            Foreground = B(Colors.Black),
                            VerticalAlignment = VerticalAlignment.Center,
                            FontSize = 16,
                        }
                        .Bind(viewModel.SearchInfoBinding, (t, value) => t.Text = value)
                    )
                ),
                new ContentControl()
                .HideIfNull(viewModel.ContentBinding)
                .Bind(viewModel.ContentBinding, (control, value) => control.Content = value?.CreateView())
            );
        }

        internal static UIElement Create(CategorizedSearchResultsViewModel viewModel)
        {
            return new ItemsControl()
            {
                Items =
                {
                    viewModel.Categories.Select(Create)
                }
            };
        }

        internal static UIElement Create(CategoryGroupSearchResultsViewModel viewModel)
        {
            return new Expander()
            {
                Background = B(0x7988BD),
                IsExpanded = true,
                Header = new TextBlock()
                {
                    Text = viewModel.Header,
                    Margin = new Thickness(4, 6, 4, 6),
                    Foreground = B(Colors.White),
                    FontSize = 16
                },
                Content = new ContentControl()
                {
                    Content = Create(viewModel.ProjectResults)
                }
            };
        }

        internal static UIElement Create(TextSpanSearchResultViewModel viewModel)
        {
            return new Button()
            {
                Content = new StackPanel()
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(8, 1, 0, 1),
                    Children =
                    {
                        new TextBlock()
                        {
                            FontFamily = Consolas,
                            FontSize = 13,
                            TextWrapping = TextWrapping.NoWrap,

                            Text = viewModel.LineNumber.ToString(),
                            Foreground = B(0x1791AF),
                        }
                        .WithMargin(new Thickness(0, 0, 16, 0)),
                        new TextBlock()
                        {
                            FontFamily = Consolas,
                            FontSize = 13,
                            TextWrapping = TextWrapping.NoWrap,

                            Text = viewModel.PrefixText,
                        },
                        new Border()
                        {
                            Background = B(Colors.Yellow),
                            Padding = new Thickness(0),
                            Margin  = new Thickness(0),
                            Child =new TextBlock()
                            {
                                FontFamily = Consolas,
                                FontSize = 13,
                                TextWrapping = TextWrapping.NoWrap,

                                Text = viewModel.ContentText,
                            }
                        },
                        new TextBlock()
                        {
                            FontFamily = Consolas,
                            FontSize = 13,
                            TextWrapping = TextWrapping.NoWrap,

                            Text = viewModel.SuffixText,
                        }
                    }
                }
            }
            .OnExecute(() => viewModel.OnExecuted());
        }

        internal static UIElement Create(ProjectGroupResultsViewModel viewModel)
        {
            return new Expander()
            {
                Margin = new Thickness(0, 0, 0, 16),
                IsExpanded = true,
                Header = new TextBlock()
                {
                    Text = viewModel.ProjectName,
                    Margin = new Thickness(5),
                    Foreground = B(Colors.Black),
                    FontSize = 18,
                    FontWeight = FontWeights.Bold
                },
                Content = new ItemsControl()
                {
                    Items =
                    {
                        viewModel.Items.Select(i => i.CreateView())
                    }
                }
            };
        }

        internal static UIElement Create(FileResultsViewModel viewModel)
        {
            return new HeaderedContentControl
            {
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Header = new Border()
                {
                    Background = B(0xf6f6f6),
                    Padding = new Thickness(12, 4, 4, 4),
                    Child = new TextBlock()
                    {
                        Text = viewModel.Path,
                        Foreground = B(Colors.Gray),
                        FontSize = 16
                    }
                },
                Content = new ItemsControl()
                {
                    Items =
                    {
                        viewModel.Items.Select(i => i.CreateView())
                    }
                }
            };
        }

        internal static UIElement Create(ProjectResultsViewModel viewModel)
        {
            return new ItemsControl()
            {
                Items =
                {
                    viewModel.ProjectGroups.Select(Create)
                }
            };
        }

        internal static UIElement Create(SymbolResultViewModel viewModel)
        {
            return new Button()
            {
                Content = new StackPanel()
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(24, 4, 4, 4),
                    Children =
                    {
                        new StackPanel()
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                new TextBlock()
                                {
                                    Text = viewModel.SymbolKind,
                                    Foreground = B(Colors.Blue),
                                    Margin = new Thickness(0, 0, 5, 0),
                                    FontSize = 16
                                },
                                new TextBlock()
                                {
                                    Text = viewModel.ShortName,
                                }
                            }
                        },
                        new TextBlock()
                        {
                            Text = viewModel.ShortName,
                            Foreground = B(Colors.Silver),
                            FontSize = 14
                        }
                    }
                }
            }
            .OnExecute(() => App.GoToDefinitionExecuted(viewModel.Symbol));
        }
    }
}
