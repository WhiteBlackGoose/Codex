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
using Codex.Lucene.Framework.AutoPrefix;

namespace Codex.Lucene.Framework
{
    public class FieldMappingCodec : Lucene46Codec
    {
        static FieldMappingCodec()
        {
            PostingsFormat.SetPostingsFormatFactory(new PostingsFormatFactory());
        }

        public static void EnsureRegistered()
        {
            // Just calling this will trigger static constructor which will register factories
        }

        private readonly MappingBase typeMapping;

        private AutoPrefixPostingsFormat AutoPrefixPostingsFormat { get; }

        public FieldMappingCodec(MappingBase typeMapping)
        {
            this.typeMapping = typeMapping;

            AutoPrefixPostingsFormat = new AutoPrefixPostingsFormat();
        }

        public override PostingsFormat GetPostingsFormatForField(string field)
        {
            var fieldMapping = typeMapping[field];
            if (fieldMapping != null && fieldMapping.MappingInfo.SearchBehavior == SearchBehavior.PrefixShortName)
            {
                return AutoPrefixPostingsFormat;
            }

            return base.GetPostingsFormatForField(field);
        }

        private class PostingsFormatFactory : DefaultPostingsFormatFactory
        {
            protected override void Initialize()
            {
                PutPostingsFormatType(typeof(AutoPrefixPostingsFormat));
                base.Initialize();
            }
        }
    }

    //public class FieldMappingCodecFactory : ICodecFactory
    //{
    //    public static void Set()
    //    {
    //        Codec.SetCodecFactory(new FieldMappingCodecFactory());
    //    }

    //    public Codec GetCodec(string name)
    //    {
    //        return new FieldMappingCodec()
    //    }
    //}
}
