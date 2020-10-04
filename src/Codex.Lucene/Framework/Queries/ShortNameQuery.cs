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
using Lucene.Net.Codecs.PerField;
using Lucene.Net.Codecs;
using Lucene.Net.Codecs.Lucene46;
using Lucene.Net.Util.Packed;

namespace Codex.Lucene.Framework
{
    // TODO: Use a combination of ngrams and doc values to search terms finding fuzzy match
    // For instance the following searches should find ShortNameQuery:
    // (Results should be ordered by the length of the longest common subsequence
    // [ "ShortQuery", "NameShortQuery", "NameQuery", "Query", "ShrtNameQuery", "ShortNmeQuery" ]
    //public class ShortNameQuery : MultiTermQuery
    //{
    //    public override string ToString(string field)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    protected override TermsEnum GetTermsEnum(Terms terms, AttributeSource atts)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}
