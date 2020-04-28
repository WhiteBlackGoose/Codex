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

namespace Codex.Lucene.Framework
{
    public class FieldMappingCodec : Lucene46Codec
    {
        Mappings m;

        public override PostingsFormat GetPostingsFormatForField(string field)
        {
            return base.GetPostingsFormatForField(field);
        }
    }
}
