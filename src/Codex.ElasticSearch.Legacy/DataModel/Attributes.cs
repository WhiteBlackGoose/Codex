using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Storage.ElasticProviders;
using Newtonsoft.Json;

namespace Codex.Storage.DataModel
{
    internal class DataObjectAttribute : ObjectAttribute
    {
        public DataObjectAttribute(DataInclusionOptions option = DataInclusionOptions.None)
        {
            // Store but don't index object fields at all
            Enabled = false;

            if (!DataInclusion.HasOption(option))
            {
                // Don't store the object at all
                Ignore = true;
            }
        }
    }

    internal class DataBooleanAttribute : BooleanAttribute
    {
        public DataBooleanAttribute()
        {
            DocValues = false;
        }
    }

    internal class DataIntegerAttribute : NumberAttribute
    {
        public DataIntegerAttribute()
            : base(NumberType.Integer)
        {
            DocValues = false;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal interface IPrefixTextProperty : ITextProperty
    {
        /// <summary>
        /// Indicates whether to store property in prefix format
        /// </summary>
        [JsonProperty("use_prefix", DefaultValueHandling = DefaultValueHandling.Ignore)]
        bool UsePrefix { get; set; }
    }

    // TODO: Use this for Definition ShortName search when 
    internal class PrefixTextAttribute : SearchStringAttribute, IPrefixTextProperty
    {
        public IProperty SelfProperty => this;

        public bool UsePrefix { get; set; }

        public PrefixTextAttribute()
        {
            UsePrefix = true;
        }
    }

    internal class SearchStringAttribute : TextAttribute
    {
        public SearchStringAttribute()
        {
            IndexOptions = IndexOptions.Docs;
            Norms = false;
        }
    }

    internal class CodexKeywordAttribute : KeywordAttribute
    {
        public CodexKeywordAttribute()
        {
            DocValues = false;
            Norms = false;
        }
    }

    internal class NormalizedKeywordAttribute : KeywordAttribute
    {
        public NormalizedKeywordAttribute()
        {
            Normalizer = CustomAnalyzers.LowerCaseKeywordNormalizerName;
            Index = true;
            Norms = false;
        }
    }

    internal class SortwordAttribute : KeywordAttribute
    {
        public SortwordAttribute()
        {
            Norms = false;
        }
    }

    internal class DataStringAttribute : CodexKeywordAttribute
    {
        public DataStringAttribute()
        {
            Index = false;
        }
    }

    /// <summary>
    /// TODO: Set options for search a path
    /// </summary>
    internal class HierachicalPathAttribute : CodexKeywordAttribute
    {
        public HierachicalPathAttribute()
        {
        }
    }

    internal class FullTextAttribute : TextAttribute
    {
        public FullTextAttribute(DataInclusionOptions option)
        {
            IndexOptions = IndexOptions.Offsets;
            TermVector = TermVectorOption.WithPositionsOffsets;
            Analyzer = CustomAnalyzers.EncodedFullTextAnalyzerName;
            if (!DataInclusion.HasOption(option))
            {
                Ignore = true;
            }
        }
    }
}
