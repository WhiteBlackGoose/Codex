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
            Console.WriteLine("Grid_Loaded");
        }

        public async void SearchTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var searchString = SearchBox.Text;
                searchString = searchString.Trim();

                if (searchString.Length < 3)
                {
                    SearchInfo.Text = "Enter at least 3 characters.";
                    return;
                }

                var result = await CodexService.SearchAsync(new SearchArguments()
                {
                    SearchString = searchString
                });

                if (result.Error != null)
                {
                    SearchInfo.Text = result.Error;
                    return;
                }
                else if (result.Result?.Hits == null || result.Result.Hits.Count == 0)
                {
                    SearchInfo.Text = "No results found.";
                    return;
                }

                SearchInfo.Text = string.Empty;

                //Console.WriteLine("Search result");
                //Console.WriteLine(result.ToString());
            }
            finally
            {
                // TODO: Set visibility of search results
                SearchInfo.Visibility = string.IsNullOrEmpty(SearchInfo.Text) ?
                    Visibility.Collapsed :
                    Visibility.Visible;
            }
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
