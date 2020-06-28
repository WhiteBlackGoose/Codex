using Codex.ObjectModel;
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
            throw Placeholder.NotImplementedException();
        }
    }

    public class OneOrMany<T>
    {
        public static implicit operator OneOrMany<T>(T[] values)
        {
            throw Placeholder.NotImplementedException();
        }

        public static implicit operator OneOrMany<T>(T value)
        {
            throw Placeholder.NotImplementedException();
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
        MatchPhrase
    }

    public class CodexQueryBuilder<T>
    {
        public virtual CodexQuery<T> Term<TValue>(Mapping<T, TValue> mapping, TValue term)
        {
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

    public class CodexQuery<T>
    {
        public CodexQueryKind Kind { get; }

        public CodexQuery(CodexQueryKind kind)
        {
            Kind = kind;
        }

        public static CodexQuery<T> operator &(CodexQuery<T> leftQuery, CodexQuery<T> rightQuery)
        {
            if (leftQuery == null) return rightQuery;
            else if (rightQuery == null) return leftQuery;

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
            throw Placeholder.NotImplementedException();
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
        public Mapping<T, TValue> Mapping { get; }

        IMapping ITermQuery.Mapping => Mapping;

        public TermCodexQuery(Mapping<T, TValue> mapping, TValue tern)
            : base(CodexQueryKind.Term)
        {
            Mapping = mapping;
        }

        public TQuery CreateQuery<TQuery>(IQueryFactory<TQuery> factory)
        {
            return ((IQueryFactory<TQuery, TValue>)factory).TermQuery(Mapping, Term);
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
