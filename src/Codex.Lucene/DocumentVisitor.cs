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
    public class QueryFactory : IQueryFactory<Query>
    {
        public static readonly QueryFactory Instance = new QueryFactory();

        public Query TermQuery(MappingBase mapping, string term)
        {
            return new TermQuery(new Term(mapping.MappingInfo.FullName, term?.ToLowerInvariant()));
        }

        public Query TermQuery(MappingBase mapping, bool term)
        {
            return TermQuery(mapping, term ? bool.TrueString : bool.FalseString);
        }

        public Query TermQuery(MappingBase mapping, long term)
        {
            throw new NotImplementedException();
        }

        public Query TermQuery(MappingBase mapping, SymbolId term)
        {
            return TermQuery(mapping, term.Value);
        }

        public Query TermQuery(MappingBase mapping, DateTime term)
        {
            throw new NotImplementedException();
        }

        public Query TermQuery(MappingBase mapping, int term)
        {
            throw new NotImplementedException();
        }
    }

    public class DocumentVisitor : IVisitor
    {
        private Document doc;
        public DocumentVisitor(Document doc)
        {
            this.doc = doc;
        }

        public bool ShouldVisit(MappingInfo mapping)
        {
            if (mapping == null || (mapping.ObjectStage & ObjectStage.Index) != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Visit(MappingBase mapping, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            switch (mapping.MappingInfo.SearchBehavior.Value)
            {

                case SearchBehavior.PrefixTerm:
                case SearchBehavior.PrefixShortName:
                case SearchBehavior.PrefixFullName:
                    // TODO: implement real fields for above search behaviors
                case SearchBehavior.Term:
                case SearchBehavior.NormalizedKeyword:
                    doc.Add(new StringField(mapping.MappingInfo.FullName, value.ToLowerInvariant(), Field.Store.NO));
                    break;
                case SearchBehavior.Sortword:
                    value = value.ToLowerInvariant();
                    doc.Add(new StringField(mapping.MappingInfo.FullName, value, Field.Store.NO));
                    doc.Add(new SortedDocValuesField(mapping.MappingInfo.FullName, new BytesRef(value)));
                    break;
                case SearchBehavior.HierarchicalPath:
                    break;
                case SearchBehavior.FullText:
                    // TODO: If we don't store field. We probably need to do something against _source
                    // field for highlighting. Other option, is to just replay this field into the document
                    // when requested.
                    doc.Add(new TextField(mapping.MappingInfo.FullName, value, Field.Store.NO));
                    break;
                default:
                    break;
            }

        }

        public void Visit(MappingBase mapping, bool value)
        {
            Visit(mapping, value ? bool.TrueString : bool.FalseString);
        }

        private FieldType StringSortwordType = new FieldType(StringField.TYPE_NOT_STORED)
        {
            DocValueType = DocValuesType.SORTED
        };

        private FieldType Int64SortwordType = new FieldType(Int64Field.TYPE_NOT_STORED)
        {
            DocValueType = DocValuesType.NUMERIC
        };

        private FieldType Int32SortwordType = new FieldType(Int32Field.TYPE_NOT_STORED)
        {
            DocValueType = DocValuesType.NUMERIC
        };

        public void Visit(MappingBase mapping, long value)
        {
            switch (mapping.MappingInfo.SearchBehavior.Value)
            {
                case SearchBehavior.Term:
                    doc.Add(new Int64Field(mapping.MappingInfo.FullName, value, Field.Store.NO));
                    break;
                case SearchBehavior.Sortword:
                    doc.Add(new Int64Field(mapping.MappingInfo.FullName, value, Int64SortwordType));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public void Visit(MappingBase mapping, SymbolId value)
        {
            Visit(mapping, value.Value);
        }

        public void Visit(MappingBase mapping, DateTime value)
        {
            Visit(mapping, value.Ticks);
        }

        public void Visit(MappingBase mapping, int value)
        {
            switch (mapping.MappingInfo.SearchBehavior.Value)
            {
                case SearchBehavior.Term:
                    doc.Add(new Int32Field(mapping.MappingInfo.FullName, value, Field.Store.NO));
                    break;
                case SearchBehavior.Sortword:
                    doc.Add(new Int32Field(mapping.MappingInfo.FullName, value, Int32SortwordType));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
