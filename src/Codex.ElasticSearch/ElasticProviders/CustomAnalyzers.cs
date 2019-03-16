using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Codex.Storage.Utilities;
using Nest;

namespace Codex.Storage.ElasticProviders
{
    /// <summary>
    /// Helper class with custom analyzers.
    /// </summary>
    internal static class CustomAnalyzers
    {
        /// <summary>
        /// Project name analyzer which lowercases project name
        /// </summary>
        public static CustomAnalyzer LowerCaseKeywordAnalyzer { get; } = new CustomAnalyzer
        {
            Filter = new List<string>
            {
                // (built in) normalize to lowercase
                "lowercase",
            },
            Tokenizer = "keyword",

        };

        /// <summary>
        /// Project name analyzer which lowercases project name
        /// </summary>
        public static CustomNormalizer LowerCaseKeywordNormalizer { get; } = new CustomNormalizer
        {
            Filter = new List<string>
            {
                // (built in) normalize to lowercase
                "lowercase",

                 // (built in) normalize to ascii equivalents
                "asciifolding",
            },
        };

        /// <summary>
        /// NGramAnalyzer is useful for "partial name search".
        /// </summary>
        public static CustomAnalyzer PrefixFilterIdentifierNGramAnalyzer { get; } = new CustomAnalyzer
        {
            Filter = new List<string>
            {
                "name_gram_boundary_inserter",
                "lower_to_upper_delimiter_inserter",
                "number_to_letter_delimiter_inserter",
                "delimited_name_prefix_generator",

                "lowercase",
                
                // (built in) normalize to ascii equivalents
                "asciifolding",

                "name_gram_delimiter_remover"
            },
            Tokenizer = "keyword",
        };

        public static CustomAnalyzer PrefixFilterFullNameNGramAnalyzer { get; } = new CustomAnalyzer
        {
            Filter = new List<string>
            {
                "container_name_prefix_generator",

                // TODO: All the filters above here could be replaced with path_hierarchy tokenizer with delimiter '.' and reverse=true.

                // (built in) normalize to lowercase
                "lowercase",

                // (built in) normalize to ascii equivalents
                "asciifolding",
            },
            Tokenizer = "keyword",

        };

        public static CustomAnalyzer EncodedFullTextAnalyzer { get; } = new CustomAnalyzer
        {
            Filter = new List<string>
            {
                "standard",
                "lowercase",
            },
            Tokenizer = "standard",
            CharFilter = new List<string>
            {
                "punctuation_to_space_replacement",
            }
        };

        public static readonly IDictionary<string, ICharFilter> CharFiltersMap = new Dictionary<string, ICharFilter>()
        {
            {
                // Replace punctuation (i.e. '.' or ',') characters with a space
                // $ ^      +=`~        <>
                "punctuation_to_space_replacement",
                new PatternReplaceCharFilter()
                {
                    Pattern = "[$^+=`~<>!\"#%&'()*,-./:;?@\\[\\]\\\\_{}]",
                    //Pattern = $"[{Regex.Escape("!\"#%&'()*,-./:;?@[\\]_{}")}]",
                    Replacement = " "
                }
            }
        };

        public static readonly IDictionary<string, ITokenFilter> FiltersMap = new Dictionary<string, ITokenFilter>()
        {
            {
                // Add '@^' symbols at the beginning of string
                "name_gram_boundary_inserter",
                new PatternReplaceTokenFilter()
                {
                    Pattern = "^(.*)",
                    Replacement = "\\^$1^"
                }
            },
            {
                // Add @ symbol before upper to lower case transition
                "lower_to_upper_delimiter_inserter",
                new PatternReplaceTokenFilter()
                {
                    Pattern = "(?:(?<leading>\\p{Ll})(?<trailing>\\p{Lu}))",
                    Replacement = "${leading}@${trailing}"
                }
            },
            {
                // Add @ symbol before number to letter transition
                "number_to_letter_delimiter_inserter",
                new PatternReplaceTokenFilter()
                {
                    Pattern = "(?:(?<leading>\\d+)(?<trailing>\\w))",
                    Replacement = "${leading}@${trailing}"
                }
            },
            {
                "delimited_name_prefix_generator",
                new PathHierarchyTokenFilter()
                {
                    Delimiter = '@',
                    Reverse = true
                }
            },
            {
                "container_name_prefix_generator",
                new PathHierarchyTokenFilter()
                {
                    Delimiter = '.',
                    Reverse = true
                }
            },
            {
                // Remove @ symbol marker in preparation for
                // generating final name grams
                "name_gram_delimiter_remover",
                new PatternReplaceTokenFilter()
                {
                    Pattern = "\\@",
                    Replacement = ""
                }
            }
        };

        public static TokenFiltersDescriptor AddTokenFilters(this TokenFiltersDescriptor descriptor)
        {
            foreach (var tokenFilterEntry in FiltersMap)
            {
                descriptor = descriptor.UserDefined(tokenFilterEntry.Key, tokenFilterEntry.Value);
            }

            return descriptor;
        }

        public static CharFiltersDescriptor AddCharFilters(this CharFiltersDescriptor descriptor)
        {
            foreach (var charFilterEntry in CharFiltersMap)
            {
                descriptor = descriptor.UserDefined(charFilterEntry.Key, charFilterEntry.Value);
            }

            return descriptor;
        }

        public const string LowerCaseKeywordNormalizerName = "lowercase_keyword_norm";
        public const string LowerCaseKeywordAnalyzerName = "lowercase_keyword";
        public const string PrefixFilterFullNameNGramAnalyzerName = "full_name";
        public const string PrefixFilterPartialNameNGramAnalyzerName = "partial_name";
        public const string EncodedFullTextAnalyzerName = "encoded_full_text";

        /// <summary>
        /// Capture filter splits incoming sequence into the tokens that would be used by the following analysis.
        /// This means that uploading the "StringBuilder" we'll get following set of indices: "str", "stri", ... "stringbuilder", "bui", "build", ... "builder.
        /// To test tokenizer, you can use following query in sense:
        /// <code>
        /// GET testsources/_analyze?tokenizer=keyword&analyzer=partial_name { "StringBuilder"}
        /// </code>
        /// where 'testsources' is an index with an uploaded source.
        /// </summary>
        public static TokenFilterBase CamelCaseFilter => new PatternCaptureTokenFilter()
        {
            PreserveOriginal = true,
            Patterns = new[]
            {
                  // Lowercase sequence with at least three lower case characters
                  "(\\p{Lu}\\p{Lu}\\p{Lu}+)",

                  // Uppercase followed by lowercase then rest of word characters (NOTE: word characters include underscore '_')
                  "(\\p{Lu}\\p{Ll}\\w+)",

                  // Non-alphanumeric char (not captured) followed by series of word characters (NOTE: word characters include underscore '_')
                  "[^\\p{L}\\d]+(\\w+)",

                  // Alphanumeric char (not captured) followed by series of at least one alpha-number then series of word characters
                  // (NOTE: word characters include underscore '_')
                  "[\\p{L}\\d]([^\\p{L}\\d]+\\w+)",

                  // Sequence of digits
                  "(\\d\\d+)"
            },
        };

        public const int MinGram = 2;
        public const int MaxGram = 70;

        /// <summary>
        /// Func that can be used with <see cref="CreateIndexExtensions.CreateIndexAsync"/>.
        /// </summary>
        public static Func<CreateIndexDescriptor, CreateIndexDescriptor> AddNGramAnalyzerFunc { get; } =
            c => c.Settings(AddAnalyzerSettings);

        public static IndexSettingsDescriptor AddAnalyzerSettings(this IndexSettingsDescriptor isd)
        {
            return isd.Analysis(descriptor => descriptor
                            .TokenFilters(tfd => AddTokenFilters(tfd))
                            .CharFilters(cfd => AddCharFilters(cfd))
                            .Normalizers(bases => bases
                                .UserDefined(LowerCaseKeywordNormalizerName, LowerCaseKeywordNormalizer))
                            .Analyzers(bases => bases
                                .UserDefined(PrefixFilterPartialNameNGramAnalyzerName, PrefixFilterIdentifierNGramAnalyzer)
                                .UserDefined(PrefixFilterFullNameNGramAnalyzerName, PrefixFilterFullNameNGramAnalyzer)
                                .UserDefined(LowerCaseKeywordAnalyzerName, LowerCaseKeywordAnalyzer)
                                .UserDefined(EncodedFullTextAnalyzerName, EncodedFullTextAnalyzer)));
        }

        private class PathHierarchyTokenFilter : PathHierarchyTokenizer, ITokenFilter
        {
            public PathHierarchyTokenFilter()
            {
                Type = "edge_ngram_delimited";
                Delimiter = '\\';
            }
        }
    }
}