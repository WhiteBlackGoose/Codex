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

        public static Orientation GetHeaderOrientation(DependencyObject obj)
        {
            return (Orientation)obj.GetValue(HeaderOrientationProperty);
        }

        public static void SetHeaderOrientation(DependencyObject obj, Orientation value)
        {
            obj.SetValue(HeaderOrientationProperty, value);
        }

        // Using a DependencyProperty as the backing store for HeaderOrientation.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HeaderOrientationProperty =
            DependencyProperty.RegisterAttached("HeaderOrientation", typeof(Orientation), typeof(Styles), new PropertyMetadata(Orientation.Horizontal));

        public static void Initialize()
        {
        }
    }
}
