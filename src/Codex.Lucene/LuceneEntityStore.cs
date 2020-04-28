using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Storage.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Search;
using Lucene.Net.Util;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;

namespace Codex.Lucene.Search
{
    public interface ILuceneEntityStore
    {
        Task InitializeAsync();

        Task FinalizeAsync();
    }

    public class LuceneEntityStore<T> : ILuceneEntityStore
        where T : class, ISearchEntity
    {
        private LuceneCodexStore Store { get; }
        private SearchType<T> SearchType { get; }
        private IndexWriter Writer { get; set; }

        public LuceneEntityStore(LuceneCodexStore store, SearchType<T> searchType)
        {
            Store = store;
            SearchType = searchType;
        }

        public async Task InitializeAsync()
        {
            await Task.Yield();

            Writer = new IndexWriter(
                    FSDirectory.Open(Store.Configuration.Directory),
                    new IndexWriterConfig(LuceneVersion.LUCENE_48, new StandardAnalyzer(LuceneVersion.LUCENE_48)));
        }

        public async Task FinalizeAsync()
        {
            await Task.Yield();

            Writer.Dispose();
        }

        public async ValueTask AddAsync(T entity)
        {
            var document = new Document();

            Placeholder.Todo("Add fields to document");

            document.Add(new StoredField(LuceneConstants.SourceFieldName, GetSource(entity)));

            Writer.AddDocument(document);
        }

        private BytesRef GetSource(T entity)
        {
            throw Placeholder.NotImplementedException("Serialize entity. Should byte[] be pooled?");
        }
    }
}
