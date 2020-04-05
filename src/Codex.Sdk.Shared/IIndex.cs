using Codex.ObjectModel;
using System;
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
    }

    public class CodexQueryBuilder<T>
    {
        public CodexQuery<T> Term<TValue>(Mapping<T, TValue> mapping, TValue term) => throw new NotImplementedException();

        public CodexQuery<T> Terms<TValue>(Mapping<T, TValue> mapping, IEnumerable<TValue> terms) => throw new NotImplementedException();

        public CodexQuery<T> MatchPhrase(Mapping<T, string> mapping, string phrase) => throw new NotImplementedException();

        public CodexQuery<T> MatchPhrasePrefix(Mapping<T, string> mapping, string phrase, int maxExpansions) => throw new NotImplementedException();
    }

    public class CodexQuery<T>
    {
        public CodexQueryKind Kind { get; protected set; }

        public static CodexQuery<T> operator &(CodexQuery<T> leftQuery, CodexQuery<T> rightQuery)
        {
            return new BinaryCodexQuery<T>(CodexQueryKind.And, leftQuery, rightQuery);
        }

        public static CodexQuery<T> operator |(CodexQuery<T> leftQuery, CodexQuery<T> rightQuery)
        {
            return new BinaryCodexQuery<T>(CodexQueryKind.Or, leftQuery, rightQuery);
        }

        public static CodexQuery<T> operator !(CodexQuery<T> query)
        {
            throw Placeholder.NotImplementedException();
        }
    }

    public class TermCodexQuery<T> : CodexQuery<T>
    {
        public TermCodexQuery(string term)
        {
            Kind = CodexQueryKind.Term;
        }
    }

    public class BinaryCodexQuery<T> : CodexQuery<T>
    {
        private CodexQueryKind and;
        private CodexQuery<T> leftQuery;
        private CodexQuery<T> rightQuery;

        public BinaryCodexQuery(CodexQueryKind and, CodexQuery<T> leftQuery, CodexQuery<T> rightQuery)
        {
            this.and = and;
            this.leftQuery = leftQuery;
            this.rightQuery = rightQuery;
        }
    }
}
