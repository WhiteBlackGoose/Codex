using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Codex.View
{
    /// <summary>
    /// Interaction logic for LeftPane.xaml
    /// </summary>
    public partial class LeftPaneView : UserControl
    {
        public LeftPaneView()
        {
            InitializeComponent();
        }

        public LeftPaneViewModel ViewModel
        {
            get { return (LeftPaneViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ViewModelProperty = 
            ViewUtilities.RegisterDependencyProperty<LeftPaneView, LeftPaneViewModel>("ViewModelProperty", onPropertyChanged: OnViewModelChanged);

        private static void OnViewModelChanged(LeftPaneView view, LeftPaneViewModel viewModel)
        {
            view.ContextGrid.DataContext = viewModel;
            view.PaneContent?.RenderContent(view, viewModel?.Content);
        }
    }
}
