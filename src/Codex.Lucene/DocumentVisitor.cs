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
    public class DocumentVisitor : IVisitor
    {
        private Document doc;
        public DocumentVisitor()
        {
        }

        public bool ShouldVisit(MappingInfo mapping)
        {
            throw new NotImplementedException();
        }

        public void Visit(MappingBase mapping, string value)
        {
            switch (mapping.MappingInfo.SearchBehavior.Value)
            {
                case SearchBehavior.Term:
                    break;
                case SearchBehavior.NormalizedKeyword:
                    break;
                case SearchBehavior.Sortword:
                    break;
                case SearchBehavior.HierarchicalPath:
                    break;
                case SearchBehavior.FullText:
                    break;
                case SearchBehavior.PrefixTerm:
                    break;
                case SearchBehavior.PrefixShortName:
                    break;
                case SearchBehavior.PrefixFullName:
                    break;
                default:
                    break;
            }
        }

        public void Visit(MappingBase mapping, bool value)
        {
            throw new NotImplementedException();
        }

        private FieldType LongSortwordType = new FieldType(Int64Field.TYPE_NOT_STORED)
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
                    doc.Add(new Int64Field(mapping.MappingInfo.FullName, value, LongSortwordType));
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
            throw new NotImplementedException();
        }

        public void Visit(MappingBase mapping, int value)
        {
            throw new NotImplementedException();
        }
    }
}
