using Codex.ObjectModel;
using Codex.Sdk.Search;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Codex.View
{
    public partial class TextSpanSearchResultViewModel : FileItemResultViewModel
    {
        public ICommand Command { get; }
        public ITextLineSpanResult TextResult;
        public IReferenceSearchResult ReferenceResult;
        public object SearchResult { get; }

        public int LineNumber { get; }
        public string PrefixText { get; }
        public string ContentText { get; }
        public string SuffixText { get; }

        public TextSpanSearchResultViewModel(ITextLineSpanResult result)
        {
            Command = Commands.GoToSpan;
            TextResult = result;
            SearchResult = result;
            var referringSpan = result.TextSpan;
            LineNumber = referringSpan.LineNumber;
            string lineSpanText = referringSpan.LineSpanText;
            if (lineSpanText != null)
            {
                PrefixText = lineSpanText.Substring(0, referringSpan.LineSpanStart);
                ContentText = lineSpanText.Substring(referringSpan.LineSpanStart, referringSpan.Length);
                SuffixText = lineSpanText.Substring(referringSpan.LineSpanStart + referringSpan.Length);
            }
        }

        public TextSpanSearchResultViewModel(IReferenceSearchResult result)
        {
            Command = Commands.GoToReference;
            ReferenceResult = result;
            SearchResult = result;
            var referringSpan = result.ReferenceSpan;
            LineNumber = referringSpan.LineNumber;
            string lineSpanText = referringSpan.LineSpanText;
            if (lineSpanText != null)
            {
                PrefixText = lineSpanText.Substring(0, referringSpan.LineSpanStart);
                ContentText = lineSpanText.Substring(referringSpan.LineSpanStart, referringSpan.Length);
                SuffixText = lineSpanText.Substring(referringSpan.LineSpanStart + referringSpan.Length);
            }
        }
    }

    public partial class ProjectItemResultViewModel
    {
    }

    public partial class FileItemResultViewModel
    {
    }

    public partial class FileResultsViewModel : ProjectItemResultViewModel, IResultsStats
    {
        public Counter Counter { get; } = new Counter();
        public string Path { get; set; }
        public IReadOnlyList<FileItemResultViewModel> Items { get; set; }
    }

    public partial class SymbolResultViewModel : ProjectItemResultViewModel
    {
        public IDefinitionSymbol Symbol { get; }
        public string ShortName { get; set; }
        public string DisplayName { get; set; }
        public string SymbolKind { get; set; }
        public string ProjectId { get; set; }

        public string ImageMoniker { get; set; }
        public int SortOrder { get; set; }
        public int IdentifierLength => ShortName.Length;

        public SymbolResultViewModel(IDefinitionSymbol symbol)
        {
            Symbol = symbol;
            ShortName = symbol.ShortName;
            DisplayName = symbol.DisplayName;
            ProjectId = symbol.ProjectId;
            SymbolKind = symbol.Kind?.ToLowerInvariant();
        }
    }

    public partial class ProjectGroupResultsViewModel : IResultsStats
    {
        public Counter Counter { get; } = new Counter();

        public string ProjectName { get; set; }
        public IReadOnlyList<ProjectItemResultViewModel> Items { get; set; }
    }

    public partial class ProjectResultsViewModel : LeftPaneContent, IResultsStats
    {
        public Counter Counter { get; } = new Counter();

        public List<ProjectGroupResultsViewModel> ProjectGroups { get; set; }

        public ProjectResultsViewModel()
        {
            ProjectGroups = new List<ProjectGroupResultsViewModel>();
        }

        public ProjectResultsViewModel(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            ProjectGroups = response.Result.Hits.Select(sr => sr.Definition).GroupBy(sr => sr.ProjectId).Select(projectGroup =>
            {
                var projectCounter = new Counter();
                return new ProjectGroupResultsViewModel()
                {
                    ProjectName = projectGroup.Key,
                    Items = projectGroup.Select(symbol => new SymbolResultViewModel(symbol).Increment(projectCounter)).ToList()
                }
                .AddFrom(projectCounter)
                .AddTo(Counter);
            }).ToList();
        }
    }

    public partial class CategoryGroupSearchResultsViewModel : IResultsStats
    {
        public Counter Counter { get; } = new Counter();

        public Visibility HeaderVisibility => string.IsNullOrEmpty(Header) ? Visibility.Collapsed : Visibility.Visible;

        public string Header { get; }

        public ProjectResultsViewModel ProjectResults { get; set; } = new ProjectResultsViewModel();

        public CategoryGroupSearchResultsViewModel(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            var result = response.Result;

            PopulateProjectGroups(result.Hits.Select(sr => sr.TextLine), sr => new TextSpanSearchResultViewModel(sr));
            Header = $"{result.Hits.Count} text search hits for '{searchString}'";
        }

        public CategoryGroupSearchResultsViewModel(ReferenceKind kind, string symbolName, IEnumerable<IReferenceSearchResult> references)
        {
            PopulateProjectGroups(references, sr => new TextSpanSearchResultViewModel(sr));
            Header = ViewUtilities.GetReferencesHeader(kind, references.Count(), symbolName);
        }

        private void PopulateProjectGroups<T>(IEnumerable<T> items, Func<T, TextSpanSearchResultViewModel> viewModelFactory) where T : IProjectFileScopeEntity
        {
            ProjectResults.ProjectGroups.AddRange(items.GroupBy(sr => sr.ProjectId).Select(projectGroup => new ProjectGroupResultsViewModel()
            {
                ProjectName = projectGroup.Key,
                Items = projectGroup.GroupBy(sr => sr.ProjectRelativePath).Select(fileGroup => new FileResultsViewModel()
                {
                    Path = fileGroup.Key,
                    Items = fileGroup.Select(sr => viewModelFactory(sr)).ToList()
                }.Add(f => f.Items.Count).AddTo(Counter)).ToList()
            }));
        }
    }

    public partial class CategorizedSearchResultsViewModel : LeftPaneContent, IResultsStats
    {
        public List<CategoryGroupSearchResultsViewModel> Categories { get; }

        public Counter Counter { get; } = new Counter();

        public CategorizedSearchResultsViewModel(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            Categories = new List<CategoryGroupSearchResultsViewModel>()
            {
                new CategoryGroupSearchResultsViewModel(searchString, response).AddTo(Counter)
            };
        }

        public CategorizedSearchResultsViewModel(string symbolName, IEnumerable<IReferenceSearchResult> references)
        {
            Categories = references.GroupBy(r => r.ReferenceSpan.Reference.ReferenceKind.ToLower()).Select(referenceGroup =>
            {
                ReferenceKind referenceKind;
                Enum.TryParse(referenceGroup.Key, true, out referenceKind);
                return new CategoryGroupSearchResultsViewModel(referenceKind, symbolName, referenceGroup).AddTo(Counter);
            }).ToList();
        }
    }

    public abstract partial class LeftPaneContent
    {
    }

    public partial class LeftPaneViewModel : PaneViewModelBase
    {
        public Visibility SearchInfoVisibility => !string.IsNullOrEmpty(SearchInfo) ? Visibility.Visible : Visibility.Collapsed;

        public string SearchInfo { get; set; } = null;

        public Visibility ContentVisibility => Content != null ? Visibility.Visible : Visibility.Collapsed;

        public LeftPaneContent Content { get; set; }

        public static readonly LeftPaneViewModel Initial = new LeftPaneViewModel()
        {
            SearchInfo = "Enter a search string. Start with ` for full text search results only."
        };

        public static LeftPaneViewModel FromReferencesResponse(IndexQueryResponse<ReferencesResult> response)
        {
            if (response.Error != null)
            {
                return new LeftPaneViewModel()
                {
                    SearchInfo = response.Error
                };
            }
            else if (response.Result?.Hits == null || response.Result.Hits.Count == 0)
            {
                return new LeftPaneViewModel()
                {
                    SearchInfo = $"No references found."
                };
            }

            var result = response.Result;
            return new LeftPaneViewModel()
            {
                Content = new CategorizedSearchResultsViewModel(response.Result.SymbolDisplayName ?? response.Result.Hits[0].ReferenceSpan.Reference.Id.Value, response.Result.Hits),
                SearchInfo = null
            };
        }

        public static LeftPaneViewModel FromSearchResponse(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            if (response.Error != null)
            {
                return new LeftPaneViewModel()
                {
                    SearchInfo = response.Error
                };
            }
            else if (response.Result?.Hits == null || response.Result.Hits.Count == 0)
            {
                return new LeftPaneViewModel()
                {
                    SearchInfo = $"No results found."
                };
            }

            var result = response.Result;
            bool isDefinitionsResult = result.Hits[0].Definition != null;
            return new LeftPaneViewModel()
            {
                Content = isDefinitionsResult ?
                    (LeftPaneContent)new ProjectResultsViewModel(searchString, response) :
                    new CategorizedSearchResultsViewModel(searchString, response),
                SearchInfo = isDefinitionsResult ?
                    (result.Hits.Count < result.Total ?
                        $"Displaying top {result.Hits.Count} results out of {result.Total}:" :
                        $"{result.Hits.Count} results found:")
                    : string.Empty
            };
        }
    }

    public partial class RightPaneViewModel : PaneViewModelBase
    {
        public Visibility ErrorVisibility => !string.IsNullOrEmpty(Error) ? Visibility.Visible : Visibility.Collapsed;

        public string Error { get; }

        public Visibility EditorVisibility => SourceFile != null ? Visibility.Visible : Visibility.Hidden;

        public IBoundSourceFile SourceFile { get; }

        public BindableValue<ILineSpan> TargetSpan { get; } = new BindableValue<ILineSpan>();

        public RightPaneViewModel()
        {
        }

        public RightPaneViewModel(IndexQueryResponse<IBoundSourceFile> sourceFileResponse)
        {
            Error = sourceFileResponse.Error;
            SourceFile = sourceFileResponse.Result;
        }

        public RightPaneViewModel(IndexQueryResponse response)
        {
            Error = response.Error;
        }

        public RightPaneViewModel(IBoundSourceFile sourceFile)
        {
            SourceFile = sourceFile;
        }
    }

    public class BindableValue<T> : NotifyPropertyChangedBase
    {
        private T value;

        public T Value
        {
            get
            {
                return value;
            }

            set
            {
                this.value = value;
                OnPropertyChanged();
            }
        }
    }


    public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        protected void OnPropertyChanged([CallerMemberName] string memberName = null)
        {
            propertyChanged?.Invoke(this, new PropertyChangedEventArgs(memberName));
        }

        private event PropertyChangedEventHandler propertyChanged;
        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                propertyChanged += value;
            }

            remove
            {
                propertyChanged -= value;
            }
        }
    }

    public class PaneViewModelBase : NotifyPropertyChangedBase
    {
        public ViewModelDataContext DataContext { get; set; }

        public PaneViewModelBase()
        {
            Initialize();
        }

        protected virtual void Initialize() { }
    }

    public interface IResultsStats
    {
        Counter Counter { get; }
    }

    public class Counter
    {
        public Counter Parent;
        public int Count;

        public Counter CreateChild()
        {
            return new Counter() { Parent = this };
        }

        public void Increment()
        {
            Count++;
        }

        public void Add(int value)
        {
            Count = value;
        }
    }

    public class ViewModelDataContext : NotifyPropertyChangedBase
    {
        private LeftPaneViewModel leftPane;

        public LeftPaneViewModel LeftPane
        {
            get
            {
                return leftPane;
            }

            set
            {
                leftPane = value;
                OnPropertyChanged();
            }
        }

        private RightPaneViewModel rightPane = new RightPaneViewModel();

        public RightPaneViewModel RightPane
        {
            get
            {
                return rightPane;
            }

            set
            {
                rightPane = value;
                OnPropertyChanged();
            }
        }

        public void Initialize()
        {
            LeftPane = LeftPaneViewModel.Initial;
        }
    }
}
