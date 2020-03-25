using System;
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

        public IBoundSourceFile SourceFile
        {
            get { return (IBoundSourceFile)GetValue(SourceFileProperty); }
            set { SetValue(SourceFileProperty, value); }
        }

        public static readonly DependencyProperty SourceFileProperty =
            DependencyProperty.Register("SourceFile", typeof(IBoundSourceFile), typeof(EditorHostControl), new PropertyMetadata(null, OnSourceFileChanged));

        private static void OnSourceFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as EditorHostControl)?.OnSourceFileChanged();
        }

        partial void OnSourceFileChanged();
    }
}
