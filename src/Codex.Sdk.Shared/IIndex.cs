using Codex.ObjectModel;
using Codex.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Sdk.Search
{
    public partial interface IIndex
    {
    }

    public interface IStoredFilterInfo
    {

    }

    public partial interface IIndex<T>
    {
        Task<IReadOnlyList<T>> GetAsync(
            IStoredFilterInfo storedFilterInfo,
            params string[] ids);

        Task<IIndexSearchResponse<TResult>> QueryAsync<TResult>(
            IStoredFilterInfo storedFilterInfo,
            Func<CodexQueryBuilder<T>, CodexQuery<T>> filter,
            OneOrMany<Mapping<T>> sort = null,
            int? take = null,
            Func<CodexQueryBuilder<T>, CodexQuery<T>> boost = null)
        where TResult : T;
    }

    public static partial class IIndexExtensions
    {
        public static Task<IIndexSearchResponse<T>> SearchAsync<T>(
            this IIndex<T> index,
            IStoredFilterInfo storedFilterInfo,
            Func<CodexQueryBuilder<T>, CodexQuery<T>> filter,
            OneOrMany<Mapping<T>> sort = null,
            int? take = null,
            Func<CodexQueryBuilder<T>, CodexQuery<T>> boost = null)
        {
            return index.QueryAsync<T>(
                storedFilterInfo,
                filter,
                sort,
                take,
                boost);
        }
    }

    public class OneOrMany<T>
    {
        public T[] Values { get; }

        public OneOrMany(params T[] values)
        {
            Values = values;
        }

        public static implicit operator OneOrMany<T>(T[] values)
        {
            return new OneOrMany<T>(values);
        }

        public static implicit operator OneOrMany<T>(T value)
        {
            return new OneOrMany<T>(value);
        }
    }

    public class AdditionalSearchArguments<T>
    {
        public Func<CodexQueryBuilder<T>, CodexQuery<T>> Boost { get; set; }
        public List<Mapping<T>> SortFields { get; } = new List<Mapping<T>>();
        public int? Take { get; set; }
    }

    public interface IIndexSearchResponse<out T>
    {
        IReadOnlyCollection<ISearchHit<T>> Hits { get; }
        public int Total { get; }
    }

    public class IndexSearchResponse<T> : IIndexSearchResponse<T>
    {
        public List<SearchHit<T>> Hits { get; set; } = new List<SearchHit<T>>();

        public int Total { get; set; }

        IReadOnlyCollection<ISearchHit<T>> IIndexSearchResponse<T>.Hits => Hits;
    }

    public class SearchHit<T> : ISearchHit<T>
    {
        public T Source { get; set; }

        public IEnumerable<TextLineSpan> Highlights { get; set; } = CollectionUtilities.Empty<TextLineSpan>.Array;
    }

    public interface ISearchHit<out T>
    {
        public T Source { get; }
        public IEnumerable<TextLineSpan> Highlights { get; }
    }

    public enum CodexQueryKind
    {
        And,
        Or,
        Term,
        Negate,
        MatchPhrase
    }

    public class CodexQueryBuilder<T>
    {
        public virtual CodexQuery<T> Term<TValue>(Mapping<T, TValue> mapping, TValue term)
        {
            if (term == null)
            {
                return null;
            }

            return new TermCodexQuery<T, TValue>(mapping, term);
        }

        public virtual CodexQuery<T> MatchPhrase(Mapping<T, string> mapping, string phrase)
        {
            return MatchPhrasePrefix(mapping, phrase, maxExpansions: 0);
        }

        public virtual CodexQuery<T> MatchPhrasePrefix(Mapping<T, string> mapping, string phrase, int maxExpansions)
        {
            return new MatchPhraseCodexQuery<T>(mapping, phrase, maxExpansions);
        }

        public virtual CodexQuery<T> Terms<TValue>(Mapping<T, TValue> mapping, IEnumerable<TValue> terms)
        {
            CodexQuery<T> q = null;
            foreach (var term in terms)
            {
                q |= Term(mapping, term);
            }

            return q;
        }
    }

    public static class CodexQueryBuilderExtensions
    {
        public static CodexQuery<T> Term<T>(this CodexQueryBuilder<T> cq, Mapping<T, SymbolId> mapping, string value)
        {
            if (value == null)
            {
                return null;
            }

            return cq.Term(mapping, SymbolId.UnsafeCreateWithValue(value));
        }
    }

    public class CodexQuery<T>
    {
        public CodexQueryKind Kind { get; }

        public CodexQuery(CodexQueryKind kind)
        {
            Kind = kind;
        }

        public static CodexQuery<T> operator &(CodexQuery<T> leftQuery, CodexQuery<T> rightQuery)
        {
            if (leftQuery == null) return rightQuery ?? null;
            else if (rightQuery == null) return leftQuery ?? null;

            return new BinaryCodexQuery<T>(CodexQueryKind.And, leftQuery, rightQuery);
        }

        public static CodexQuery<T> operator |(CodexQuery<T> leftQuery, CodexQuery<T> rightQuery)
        {
            if (leftQuery == null) return rightQuery;
            else if (rightQuery == null) return leftQuery;

            return new BinaryCodexQuery<T>(CodexQueryKind.Or, leftQuery, rightQuery);
        }

        public static CodexQuery<T> operator !(CodexQuery<T> query)
        {
            if (query == null) return null;

            return new NegateCodexQuery<T>(query);
        }
    }

    public interface ITermQuery
    {
        IMapping Mapping { get; }

        TQuery CreateQuery<TQuery>(IQueryFactory<TQuery> factory);
    }

    public class TermCodexQuery<T, TValue> : CodexQuery<T>, ITermQuery
    {
        public TValue Term { get; }
        public MappingBase Mapping { get; }

        IMapping ITermQuery.Mapping => Mapping;

        public TermCodexQuery(MappingBase mapping, TValue term)
            : base(CodexQueryKind.Term)
        {
            Mapping = mapping;
            Term = term;
        }

        public TQuery CreateQuery<TQuery>(IQueryFactory<TQuery> factory)
        {
            return ((IQueryFactory<TQuery, TValue>)factory).TermQuery(Mapping, Term);
        }
    }

    public class NegateCodexQuery<T> : CodexQuery<T>
    {
        public CodexQuery<T> InnerQuery { get; }

        public NegateCodexQuery(CodexQuery<T> innerQuery)
            : base(CodexQueryKind.Negate)
        {
            InnerQuery = innerQuery;
        }
    }

    public class BinaryCodexQuery<T> : CodexQuery<T>
    {
        public CodexQuery<T> LeftQuery { get; }
        public CodexQuery<T> RightQuery { get; }

        public BinaryCodexQuery(CodexQueryKind kind, CodexQuery<T> leftQuery, CodexQuery<T> rightQuery)
            : base(kind)
        {
            this.LeftQuery = leftQuery;
            this.RightQuery = rightQuery;
        }
    }

    public class MatchPhraseCodexQuery<T> : CodexQuery<T>
    {
        public string Phrase { get; }
        public int MaxExpansions { get; }
        public Mapping<T, string> Mapping { get; }

        public MatchPhraseCodexQuery(Mapping<T, string> mapping, string phrase, int maxExpansions)
            : base(CodexQueryKind.MatchPhrase)
        {
            Mapping = mapping;
            Phrase = phrase;
            MaxExpansions = maxExpansions;
        }

        //public TQuery CreateQuery<TQuery>(IQueryFactory<TQuery> factory)
        //{
        //    return ((IQueryFactory<TQuery, TValue>)factory).TermQuery(Mapping, Term);
        //}
    }
}
