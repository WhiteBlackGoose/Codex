using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Types
{
    public partial interface IIndex
    {

    }

    /// <summary>
    /// High level query operations for indexed code
    /// </summary>
    public abstract partial class Index<T>
    {
        public abstract IndexQuery<T> CreateQuery();

        public abstract IPrefixProperty<T> CreatePrefixProperty();

        public abstract ITermProperty<T> CreateTermProperty();
    }

    public abstract class IndexQuery<T>
    {
        public IndexFilter<T> Filter { get; set; }

        /// <summary>
        /// The maximum number of results to return
        /// </summary>
        public int MaxResults { get; set; }

        public abstract Task<IIndexQueryHitsResponse<T>> ExecuteAsync();
    }

    public abstract class PrefixFullNameIndexProperty<T> : TermIndexProperty<T>
    {
        public abstract IndexFilter<T> PrefixFullName(string prefix);
    }

    public abstract class PrefixIndexProperty<T> : TermIndexProperty<T>
    {
        public abstract IndexFilter<T> Prefix(string prefix);
    }

    public abstract class NormalizedKeywordIndexProperty<T> : TermIndexProperty<T>
    {
    }

    public abstract class FullTextIndexProperty<T> : TermIndexProperty<T>
    {
        public abstract IndexFilter<T> Contains(string phrase);

        public abstract IndexFilter<T> ContainsAll(string[] terms);
    }

    public abstract class SortwordIndexProperty<T> : TermIndexProperty<T>
    {

    }


    public abstract class TermIndexProperty<T>
    {
        public abstract IndexFilter<T> Match<TValue>(TValue value);
    }

    public interface IPrefixProperty<T> : ITermProperty<T>
    {
        IndexFilter<T> Prefix(string prefix);
    }

    public interface ITermProperty<T>
    {
        IndexFilter<T> Match<TValue>(TValue value);
    }

    public enum BinaryOperator
    {
        And,
        Or,
    }

    public class BinaryFilter<T> : IndexFilter<T>
    {
        public readonly BinaryOperator Operator;
        public readonly IndexFilter<T> Left;
        public readonly IndexFilter<T> Right;

        public BinaryFilter(BinaryOperator op, IndexFilter<T> left, IndexFilter<T> right)
        {
            Operator = op;
            Left = left;
            Right = right;
        }
    }

    public class IndexFilter<T>
    {
        public static IndexFilter<T> operator &(IndexFilter<T> left, IndexFilter<T> right)
        {
            return new BinaryFilter<T>(BinaryOperator.And, left, right);
        }

        public static IndexFilter<T> operator |(IndexFilter<T> left, IndexFilter<T> right)
        {
            return new BinaryFilter<T>(BinaryOperator.Or, left, right);
        }
    }

    public interface IIndexQuery<T>
    {
        IIndexQueryFilter<T> Filter { get; set; }

        /// <summary>
        /// The maximum number of results to return
        /// </summary>
        int MaxResults { get; set; }

        Task<IIndexQueryResponse<T>> ExecuteAsync();
    }

    public interface IIndexQueryResponse<T>
    {
        /// <summary>
        /// If the query failed, this will contain the error message
        /// </summary>
        string Error { get; }

        /// <summary>
        /// The raw query sent to the index server
        /// </summary>
        string RawQuery { get; }

        /// <summary>
        /// The spent executing the query
        /// </summary>
        TimeSpan QueryTime { get; }

        /// <summary>
        /// The results of the query
        /// </summary>
        T Result { get; }
    }

    public interface IIndexQueryHits<T>
    {
        /// <summary>
        /// The total number of results matching the query. 
        /// NOTE: This may be greater than the number of hits returned.
        /// </summary>
        int HitCount { get; }

        /// <summary>
        /// The results of the query
        /// </summary>
        IReadOnlyList<T> Hits { get; }
    }


    public interface IIndexQueryHitsResponse<T> : IIndexQueryResponse<IIndexQueryHits<T>>
    {

    }

    public interface IIndexQueryFilter<T>
    {
    }


}
