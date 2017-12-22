using Codex.ObjectModel;
using Codex.Sdk.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
            SearchBox.Text = "as";
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Grid_Loaded");
        }

        public async void SearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var searchString = SearchBox.Text;
            searchString = searchString.Trim();

            if (searchString == "ass")
            {
#if !BRIDGE
                //UITrace.Start();
#endif
            }
            else
            {
#if !BRIDGE
                //UITrace.Stop();
#endif
            }

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

        public async void GoToSpanExecuted(ITextLineSpanResult lineSpan)
        {
            var sourceFileResponse = await CodexService.GetSourceAsync(new GetSourceArguments()
            {
                ProjectId = lineSpan.ProjectId,
                ProjectRelativePath = lineSpan.ProjectRelativePath,
            });

            ViewModel.RightPane = new RightPaneViewModel(sourceFileResponse);
        }

        public async void GoToReferenceExecuted(IReferenceSearchResult referenceResult)
        {
            var sourceFileResponse = await CodexService.GetSourceAsync(new GetSourceArguments()
            {
                ProjectId = referenceResult.ProjectId,
                ProjectRelativePath = referenceResult.ProjectRelativePath,
            });

            ViewModel.RightPane = new RightPaneViewModel(sourceFileResponse);
        }

        public async void FindAllReferencesExecuted(IReferenceSymbol symbol)
        {
            var response = await CodexService.FindAllReferencesAsync(new FindAllReferencesArguments()
            {
                ProjectId = symbol.ProjectId,
                SymbolId = symbol.Id.Value,
            });

            ViewModel.LeftPane = LeftPaneViewModel.FromReferencesResponse(response);
        }

        public async void GoToDefinitionExecuted(IReferenceSymbol symbol)
        {
            var response = await CodexService.FindDefinitionLocationAsync(new FindDefinitionLocationArguments()
            {
                ProjectId = symbol.ProjectId,
                SymbolId = symbol.Id.Value,
            });

            if (response.Error != null || response.Result.Hits.Count == 0)
            {
                ViewModel.RightPane = new RightPaneViewModel(response);
            }
            else if (response.Result.Hits.Count > 1 || response.Result.Hits[0].ReferenceSpan.Reference.ReferenceKind != nameof(ReferenceKind.Definition))
            {
                // Show definitions in left pane
                ViewModel.LeftPane = LeftPaneViewModel.FromReferencesResponse(response);
            }
            else
            {
                IReferenceSearchResult reference = response.Result.Hits[0];
                var sourceFileResponse = await CodexService.GetSourceAsync(new GetSourceArguments()
                {
                    ProjectId = reference.ProjectId,
                    ProjectRelativePath = reference.ProjectRelativePath,
                });

                ViewModel.RightPane = new RightPaneViewModel(sourceFileResponse);
            }
        }

        public async void UpdateRightPane(Func<Task<RightPaneViewModel>> getViewModel)
        {
            var rightViewModel = await getViewModel();
            ViewModel.RightPane = rightViewModel;
        }
    }
}
