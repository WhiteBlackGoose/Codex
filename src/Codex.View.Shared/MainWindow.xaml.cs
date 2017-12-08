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

                var response = await CodexService.SearchAsync(new SearchArguments()
                {
                    SearchString = searchString
                });

                if (response.Error != null)
                {
                    SearchInfo.Text = response.Error;
                    return;
                }
                else if (response.Result?.Hits == null || response.Result.Hits.Count == 0)
                {
                    SearchInfo.Text = $"No results found\n"
                        + $"(response.Result == null):{response.Result == null}\n"
                        + $"(response.Result?.Hits == null):{response.Result?.Hits == null}\n"
                        + $"(response.Result.Hits?.Count):{response.Result.Hits?.Count}\n"
                        + $"(response.Result == null):{response.Result == null}";
                    return;
                }

                SearchInfo.Text = string.Empty;
                SearchResultsContainer.DataContext = new TextSearchResultsViewModel(searchString, response);

                //Console.WriteLine("Search result");
                //Console.WriteLine(result.ToString());
            }
            finally
            {
                if (string.IsNullOrEmpty(SearchInfo.Text))
                {
                    SearchResultsContainer.Visibility = Visibility.Visible;
                    SearchInfo.Visibility = Visibility.Collapsed;
                }
                else
                {
                    SearchResultsContainer.Visibility = Visibility.Collapsed;
                    SearchInfo.Visibility = Visibility.Visible;
                }
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
