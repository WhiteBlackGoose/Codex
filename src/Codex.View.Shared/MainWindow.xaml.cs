using Codex.Sdk.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Codex.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ICodex CodexService { get; } = CodexProvider.Instance;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Hello world");
        }

        public async void SearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var result = await CodexService.SearchAsync(new SearchArguments()
            {
                SearchString = ((TextBox)sender).Text
            });

            Console.WriteLine("Search result");
            Console.WriteLine(result.ToString());
        }

        //private void Border_SizeChanged(object sender, SizeChangedEventArgs e)
        //{
        //}

        //private void Window_Loaded(object sender, RoutedEventArgs e)
        //{
        //}

        //private void Window_Initialized(object sender, EventArgs e)
        //{
        //}

        //protected override Size ArrangeOverride(Size arrangeBounds)
        //{
        //    var result = base.ArrangeOverride(arrangeBounds);
        //    return result;
        //}
    }
}
