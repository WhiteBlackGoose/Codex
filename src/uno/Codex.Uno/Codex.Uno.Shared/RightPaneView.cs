using Codex.ObjectModel;
using Codex.View;
using Codex.Web.Mvc.Rendering;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.UI.Controls;
#if __WASM__
using Monaco;
using Monaco.Editor;
using Monaco.Languages;
#endif
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
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
                    Row(Auto, CreateDetailsPane(viewModel.SourceFile))
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
#if __WASM__
                loadOverview();

                async void loadOverview()
                {
                    var overviewPath = Path.GetFullPath("overview");

                    var file = await StorageFile.GetFileFromApplicationUriAsync(new System.Uri("ms-appx:///Assets/Overview.html"));

                    var newFile = await file.CopyAsync(Windows.Storage.ApplicationData.Current.LocalFolder, file.Name, NameCollisionOption.ReplaceExisting);

                    using var stream = await newFile.OpenStreamForReadAsync();
                    using var reader = new StreamReader(stream);

                    var text = reader.ReadToEnd();

                    control.HtmlContent = text;
                }
#endif
            }

            return control;
        }

        private static FrameworkElement CreateDetailsPane(IBoundSourceFile sourceFile)
        {
            var info = sourceFile?.SourceFile?.Info;
            if (info == null)
            {
                var random = new Random();
                return new Border()
                {
                    Height = 56,
                    Background = B(random.Next())
                };
            }

            var encodedFilePath = HttpUtility.UrlEncode(info.ProjectRelativePath);

            return new Grid()
            .WithChildren
            (
                Column(Star),
                Column(Star),
                Row(Auto),
                Row(Auto),

                Row(0, Column(0, Text(
                    "File: ", 
                    Link(info.ProjectRelativePath, $"/?leftProject={info.ProjectId}&file={encodedFilePath}"), 
                    "(", Link("Download", $"/download/{info.ProjectId}/?filePath={encodedFilePath}"), ")"))),
                Row(1, Column(0, Text(
                    "Project: ", Link(info.ProjectId, $"/?leftProject={info.ProjectId}")))),
                Row(0, Column(1, Text(Link(info.RepoRelativePath, info.WebAddress)))),
                Row(1, Column(1, Tip(
                    Text("Indexed on: ", sourceFile.Commit?.DateUploaded.ToLocalTime().ToString() ?? "Unknown"),
                    $"Index: {sourceFile.Commit?.CommitId}")))
            );
        }

        private static FrameworkElement CreateEditor(IBoundSourceFile sourceFile)
        {
            //var editor = new CodeEditor()
            //{
            //    Text = sourceFile?.SourceFile.Content ?? "EMPTY",
            //};

            //async void hookLanguage()
            //{
            //    await editor.Languages.RegisterAsync(new ILanguageExtensionPoint()
            //    {
            //        Id = "Codex"
            //    });

            //    await editor.Languages.RegisterColorProviderAsync("Codex", new CodexEditorLanguage(sourceFile));
            //    Console.WriteLine("Setting language");

            //    editor.CodeLanguage = "Codex";

            //    Console.WriteLine("Set language");

            //}
            //editor.Loaded += (s, e) =>
            //{
            //    if (editor.IsEditorLoaded)
            //    {
            //        hookLanguage();
            //    }
            //};

            var editor = new ContentControl()
            {
                Content = "NO EDITOR"
            };

            return editor;
        }

        private class CodexEditorLanguage
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
