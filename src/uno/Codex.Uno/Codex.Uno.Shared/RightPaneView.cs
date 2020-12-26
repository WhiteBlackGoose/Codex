using Codex.ObjectModel;
using Codex.View;
using Codex.Web.Mvc.Rendering;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Monaco;
using Monaco.Editor;
using Monaco.Languages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Uno.Extensions;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Codex.Uno.Shared
{
    using static ViewBuilder;
    using static MainController;

    public class RightPaneView
    {
        public static FrameworkElement Create(RightPaneViewModel viewModel)
        {

            return new Grid()
                .WithChildren(
                    Row(Star, CreateViewPane(viewModel)),
                    //Row(1, viewModel.SourceFile == null ? new Border() { Background = B(Colors.OrangeRed) } : CreateEditor(viewModel.SourceFile)),
                    Row(Auto, new Border()
                    {
                        Height = 56,
                        Background = B(Colors.Purple)
                    })
                );
        }

        private static WasmHtmlContentControl CreateViewPane(RightPaneViewModel viewModel)
        {
            var content = viewModel.SourceFile == null ? "" : new SourceFileRenderer(viewModel.SourceFile).RenderHtml();
            var control = new WasmHtmlContentControl
            {
                HtmlContent = content
            };

            if (viewModel.SourceFile == null)
            {
                loadOverview();

                async void loadOverview()
                {
                    var file = await StorageFile.GetFileFromApplicationUriAsync(new System.Uri("ms-appx:///Assets/Overview.html"));
                    var newFile = await file.CopyAsync(Windows.Storage.ApplicationData.Current.LocalFolder, file.Name, NameCollisionOption.ReplaceExisting);
                    var text = await FileIO.ReadTextAsync(newFile, Windows.Storage.Streams.UnicodeEncoding.Utf8);
                    control.HtmlContent = text;
                }
            }

            return control;
        }

        private static FrameworkElement CreateEditor(IBoundSourceFile sourceFile)
        {
            var editor = new CodeEditor()
            {
                Text = sourceFile?.SourceFile.Content ?? "EMPTY",
            };

            async void hookLanguage()
            {
                await editor.Languages.RegisterAsync(new ILanguageExtensionPoint()
                {
                    Id = "Codex"
                });

                await editor.Languages.RegisterColorProviderAsync("Codex", new CodexEditorLanguage(sourceFile));
                Console.WriteLine("Setting language");

                editor.CodeLanguage = "Codex";

                Console.WriteLine("Set language");

            }
            editor.Loaded += (s, e) =>
            {
                if (editor.IsEditorLoaded)
                {
                    hookLanguage();
                }
            };

            return editor;
        }

        private class CodexEditorLanguage : DocumentColorProvider
        {
            private static IDictionary<string, Color> ClassificationMap = new Dictionary<string, Color>
            {
                { "xml - delimiter", Constants.ClassificationXmlDelimiter },
                { "xml - name", Constants.ClassificationXmlName },
                { "xml - attribute name", Constants.ClassificationXmlAttributeName },
                { "xml - attribute quotes", Constants.ClassificationXmlAttributeQuotes },
                { "xml - attribute value", Constants.ClassificationXmlAttributeValue },
                { "xml - entity reference", Constants.ClassificationXmlEntityReference },
                { "xml - cdata section", Constants.ClassificationXmlCDataSection },
                { "xml - processing instruction", Constants.ClassificationXmlProcessingInstruction },
                { "xml - comment", Constants.ClassificationComment },

                { "keyword", Constants.ClassificationKeyword },
                { "keyword - control", Constants.ClassificationKeyword },
                { "identifier", Constants.ClassificationIdentifier },
                { "class name", Constants.ClassificationTypeName },
                { "struct name", Constants.ClassificationTypeName },
                { "interface name", Constants.ClassificationTypeName },
                { "enum name", Constants.ClassificationTypeName },
                { "delegate name", Constants.ClassificationTypeName },
                { "module name", Constants.ClassificationTypeName },
                { "type parameter name", Constants.ClassificationTypeName },
                { "preprocessor keyword", Constants.ClassificationKeyword },
                { "xml doc comment - delimiter", Constants.ClassificationComment },
                { "xml doc comment - name", Constants.ClassificationComment },
                { "xml doc comment - text", Constants.ClassificationComment },
                { "xml doc comment - comment", Constants.ClassificationComment },
                { "xml doc comment - entity reference", Constants.ClassificationComment },
                { "xml doc comment - attribute name", Constants.ClassificationComment },
                { "xml doc comment - attribute quotes", Constants.ClassificationComment },
                { "xml doc comment - attribute value", Constants.ClassificationComment },
                { "xml doc comment - cdata section", Constants.ClassificationComment },
                { "xml literal - delimiter", Constants.ClassificationXmlLiteralDelimiter },
                { "xml literal - name", Constants.ClassificationXmlLiteralName },
                { "xml literal - attribute name", Constants.ClassificationXmlLiteralAttributeName },
                { "xml literal - attribute quotes", Constants.ClassificationXmlLiteralAttributeQuotes },
                { "xml literal - attribute value", Constants.ClassificationXmlLiteralAttributeValue },
                { "xml literal - entity reference", Constants.ClassificationXmlLiteralEntityReference },
                { "xml literal - cdata section", Constants.ClassificationXmlLiteralCDataSection },
                { "xml literal - processing instruction", Constants.ClassificationXmlLiteralProcessingInstruction },
                { "xml literal - embedded expression", Constants.ClassificationXmlLiteralEmbeddedExpression },
                { "xml literal - comment", Constants.ClassificationComment },
                { "comment", Constants.ClassificationComment },
                { "string", Constants.ClassificationLiteral },
                { "string - verbatim", Constants.ClassificationLiteral },
                { "excluded code", Constants.ClassificationExcludedCode },
            };

            private static Dictionary<int, ColorPresentation[]> presentationByColorMap = new Dictionary<int, ColorPresentation[]>();

            public IBoundSourceFile SourceFile { get; }
            private string sourceText;

            public CodexEditorLanguage(IBoundSourceFile sourceFile)
            {
                SourceFile = sourceFile;
                sourceText = sourceFile?.SourceFile.Content;
            }

            public IAsyncOperation<IEnumerable<ColorPresentation>> ProvideColorPresentationsAsync(IModel model, ColorInformation colorInfo)
            {
                return AsAsyncOperation(() => Task.FromResult<IEnumerable<ColorPresentation>>(Array.Empty<ColorPresentation>()));
            }

            public IAsyncOperation<IEnumerable<ColorInformation>> ProvideDocumentColorsAsync(IModel model)
            {
                return AsAsyncOperation<IEnumerable<ColorInformation>>(() =>
                {
                    return Task.FromResult(GetColorInformation().ToList().AsEnumerable());
                });
            }

            private IEnumerable<ColorInformation> GetColorInformation()
            {
                if (sourceText == null)
                {
                    yield break;
                }

                var lineLengths = TextUtilities.GetLineLengths(sourceText);
                int position = 0;
                int line = 0;
                int column = 0;
                int lineLength = lineLengths[0];

                IClassificationSpan prevSpan = null;

                bool finished = false;

                foreach (var span in SourceFile.Classifications.OrderBy(c => c.Start))
                {
                    if (span.Start > sourceText.Length)
                    { //Not sure how this happened but a span is off the end of our text
                        Console.Error.WriteLine(
                            $"Span had Start of {span.Start}, which is greater than text length for file '{SourceFile.ProjectRelativePath}'");
                        break;
                    }
                    if (prevSpan != null && span.Start == prevSpan.Start)
                    {
                        //  Overlapping spans?
                        continue;
                    }

                    prevSpan = span;

                    //Console.WriteLine("Classifying span: ")

                    if (!ClassificationMap.TryGetValue(span.Classification, out var color) || color.A == 0)
                    {
                        continue;
                    }

                    (uint line, uint column) getEditorPosition(int targetPosition)
                    {
                        var positionOffset = targetPosition - position;
                        while (line < lineLengths.Length && positionOffset > 0)
                        {
                            var remaining = lineLength - column;
                            if (remaining > positionOffset)
                            {
                                column += positionOffset;
                                positionOffset = 0;
                                break;
                            }
                            else
                            {
                                positionOffset -= remaining;
                                column = 0;
                                line++;
                                if (line < lineLengths.Length)
                                {
                                    lineLength = lineLengths[line];
                                }
                            }
                        }

                        if (positionOffset != 0)
                        {
                            finished = true;
                            return default;
                        }

                        position = targetPosition;
                        return ((uint)line + 1, (uint)column + 1);
                    }

                    if (finished)
                    {
                        yield break;
                    }

                    var start = getEditorPosition(span.Start);
                    var end = getEditorPosition(span.End());

                    yield return new ColorInformation(color, new Range(start.line, start.column, end.line, end.column));
                }
            }

            private static IAsyncOperation<T> AsAsyncOperation<T>(Func<Task<T>> taskFactory)
            {
                return WindowsRuntimeSystemExtensions.AsAsyncOperation(taskFactory());
            }

            public class Constants
            {
                public static readonly Color ClassificationIdentifier = C(0xffff00);
                public static readonly Color ClassificationKeyword = Colors.Blue;
                public static readonly Color ClassificationTypeName = C(0x2b91af);
                public static readonly Color ClassificationComment = C(0x008000);
                public static readonly Color ClassificationLiteral = C(0xa31515);
                public static readonly Color ClassificationXmlDelimiter = Colors.Blue;
                public static readonly Color ClassificationXmlName = C(0xa31515);
                public static readonly Color ClassificationXmlAttributeName = Colors.Red;
                public static readonly Color ClassificationXmlAttributeValue = Colors.Blue;
                public static readonly Color ClassificationXmlAttributeQuotes = default;
                public static readonly Color ClassificationXmlEntityReference = Colors.Red;
                public static readonly Color ClassificationXmlCDataSection = C(0x808080);
                public static readonly Color ClassificationXmlProcessingInstruction = C(0x808080);
                public static readonly Color ClassificationXmlLiteralDelimiter = C(0x6464b9);
                public static readonly Color ClassificationXmlLiteralName = C(0x844646);
                public static readonly Color ClassificationXmlLiteralAttributeName = C(0xb96464);
                public static readonly Color ClassificationXmlLiteralAttributeValue = C(0x6464b9);
                public static readonly Color ClassificationXmlLiteralAttributeQuotes = C(0x555555);
                public static readonly Color ClassificationXmlLiteralEntityReference = C(0xb96464);
                public static readonly Color ClassificationXmlLiteralCDataSection = C(0xc0c0c0);
                public static readonly Color ClassificationXmlLiteralEmbeddedExpression = C(0xb96464);
                public static readonly Color ClassificationXmlLiteralProcessingInstruction = C(0xc0c0c0);
                public static readonly Color ClassificationExcludedCode = C(0x808080);
                public static readonly Color ClassificationPunctuation = default;
            }
        }


    }
}
