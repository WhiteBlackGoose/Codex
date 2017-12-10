using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Codex.View
{
    public static class Styles
    {
        public static Brush GetMouseOverBackground(DependencyObject obj)
        {
            return (Brush)obj.GetValue(MouseOverBackgroundProperty);
        }

        public static void SetMouseOverBackground(DependencyObject obj, Brush value)
        {
            obj.SetValue(MouseOverBackgroundProperty, value);
        }

        // Using a DependencyProperty as the backing store for MouseOverBackground.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MouseOverBackgroundProperty =
            DependencyProperty.RegisterAttached("MouseOverBackground", typeof(Brush), typeof(Styles), new PropertyMetadata(Brushes.Transparent));



        public static bool GetIsMouseOverHeader(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsMouseOverHeaderProperty);
        }

        public static void SetIsMouseOverHeader(DependencyObject obj, bool value)
        {
            obj.SetValue(IsMouseOverHeaderProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsMouseOverHeader.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsMouseOverHeaderProperty =
            DependencyProperty.RegisterAttached("IsMouseOverHeader", typeof(bool), typeof(Styles), new PropertyMetadata(false));



        public static void Initialize()
        {
        }
    }
}
