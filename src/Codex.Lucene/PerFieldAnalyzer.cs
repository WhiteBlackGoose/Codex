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
using Lucene.Net.Analysis;
using System.IO;

namespace Codex.Lucene.Search
{
    public class PerFieldAnalyzer : Analyzer
    {
        private readonly MappingBase typeMapping;


        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var fieldMapping = typeMapping[fieldName];
            switch (fieldMapping.MappingInfo.SearchBehavior)
            {
                case SearchBehavior.None:
                    break;
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

            throw Placeholder.NotImplementedException();
        }

        private class PrefixShortNameTokenizer : Tokenizer
        {
            public PrefixShortNameTokenizer(TextReader input) : base(input)
            {
            }

            public override bool IncrementToken()
            {
                throw new NotImplementedException();
            }
        }
    }
}
