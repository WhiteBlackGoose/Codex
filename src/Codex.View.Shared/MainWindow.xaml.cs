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

        private ViewModelDataContext ViewModel = new ViewModelDataContext();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = ViewModel;
            ViewModel.Initialize();
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
            var searchString = SearchBox.Text;
            searchString = searchString.Trim();

            if (searchString.Length < 3)
            {
                ViewModel.LeftPane = new LeftPaneViewModel()
                {
                    SearchInfo = "Enter at least 3 characters."
                };
                return;
            }

            var response = await CodexService.SearchAsync(new SearchArguments()
            {
                SearchString = searchString
            });

            ViewModel.LeftPane = LeftPaneViewModel.FromSearchResponse(searchString, response);
        }
    }
}
