﻿using Codex.View;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Text;
using Uno.Extensions;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Codex.Uno.Shared
{
    using static ViewBuilder;

    public class MainPageView
    {
        public static LinearGradientBrush PageHeaderBrush = new LinearGradientBrush()
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = Color(0x744482), Offset = 0 },
                new GradientStop { Color = Color(0x7db9e8), Offset = 0.8 },
                new GradientStop { Color = Color(0xb7fff9), Offset = 1 },
            }
        };

        public static FrameworkElement Create(ViewModelDataContext viewModel)
        {
            return new DockPanel().WithChildren(
                Top(
                    new Grid()
                    {
                        Height = 58,
                        Background = PageHeaderBrush,
                        ColumnDefinitions =
                        {
                            // SearchBoxAndImagesColumn
                            new ColumnDefinition() { Width = GridLength.Auto },
                            new ColumnDefinition(),
                            // HeaderMenuColumn
                            new ColumnDefinition() { Width = GridLength.Auto }
                        }
                    }.WithChildren(
                        new StackPanel { VerticalAlignment = VerticalAlignment.Stretch, Orientation = Orientation.Horizontal }.WithChildren(
                            new TextBox()
                            {
                                FontFamily = new FontFamily("Arial"),
                                FontSize = 21.333,
                                Foreground = B(Colors.Black),
                                Background = B(Colors.White),
                                VerticalContentAlignment = VerticalAlignment.Center,
                                MaxLength = 260,
                                Width = 492,
                                Padding = new Thickness(32, 5, 5, 5),
                                Margin = new Thickness(8, 8, 0, 8),
                            }
                            .OnTextChanged(text => OnSearchTextChanged(text)),
                            new Border() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10), Width = 95.5 }.WithChild(
                                new Image() { Source = RelativeImageSource("Assets/Images/microsoftlogo.png") }
                            ),
                            new TextBlock()
                            {
                                Text = "Codex",
                                Foreground = B(Colors.White),
                                VerticalAlignment = VerticalAlignment.Center,
                                FontSize = 20,
                                Width = 56,
                                Margin = new Thickness(0, 0, 0, 4)
                            }
                        )
                    )
                ),
                new Grid()
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition() { Width = new GridLength(500) },
                        new ColumnDefinition() { Width = GridLength.Auto },
                        new ColumnDefinition()
                    }
                }.WithChildren(
                    Column(0,
                        LeftPaneView.Create(viewModel.LeftPane)
                    ),
                    Column(1,
                        new GridSplitter()
                        {
                            Background = B(Colors.Gainsboro),
                            VerticalAlignment = VerticalAlignment.Stretch,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Width = 20
                        }
                    ),
                    Column(2,
                        LeftPaneView.Create(viewModel.LeftPane)
                    )
                )
            );
        }

        private static void OnSearchTextChanged(string text)
        {
            throw new NotImplementedException();
        }
    }
}
