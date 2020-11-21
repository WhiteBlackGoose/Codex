using Codex.Lucene.Search;
using Codex.View;
using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using Uno.UI.Wasm;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Codex.Uno.Wasm
{
    public class Program
    {
        private static App _app;

        public static int Main(string[] args)
        {
            LoadIndex();

            Windows.UI.Xaml.Application.Start(_ => _app = new App());

            return 0;
        }

        public static async void LoadIndex()
        {
            var lucenePath = Path.GetFullPath("lucene");
            Console.WriteLine($"LoadIndex: lucenePath={lucenePath}");

            var file = await StorageFile.GetFileFromApplicationUriAsync(new System.Uri("ms-appx:///Assets/testindex.zip"));

            Console.WriteLine($"LoadIndex: Retrieved File");

            var newFile = await file.CopyAsync(Windows.Storage.ApplicationData.Current.LocalFolder, file.Name, NameCollisionOption.ReplaceExisting);

            Console.WriteLine($"LoadIndex: Downloaded File");

            using (var stream = await newFile.OpenStreamForReadAsync())
            using (ZipArchive archive = new ZipArchive(stream))
            {
                archive.ExtractToDirectory(lucenePath);
            }

            Console.WriteLine($"LoadIndex: files.length={Directory.GetFiles(lucenePath, "*.*", SearchOption.AllDirectories).Length}");

            MainController.App.CodexService = new LuceneCodex
            (
                new LuceneConfiguration(lucenePath)
            );
        }
    }
}
