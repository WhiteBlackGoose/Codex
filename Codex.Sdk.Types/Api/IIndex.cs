using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Types
{
    /// <summary>
    /// High level query operations for indexed code
    /// </summary>
    public interface IIndex<T>
    {
        IndexQuery<T> CreateQuery();
    }

    public abstract class Index
    {
        public abstract IndexQuery<T> CreateQuery<T>();
    }

    public static class FilterAdapters
    {
        public static PrefixIndexProperty<T> AsPrefix<T>(this object o)
        {
            throw new NotImplementedException();
        }

        public static IndexProperty<T> AsTerm<T>(this object o)
        {
            throw new NotImplementedException();
        }
    }

    public abstract class IndexQuery<T>
    {
        public IndexFilter<T> Filter { get; set; }

        /// <summary>
        /// The maximum number of results to return
        /// </summary>
        public int MaxResults { get; set; }

        public abstract Task<IIndexQueryResult<T>> ExecuteAsync();
    }

    public abstract class PrefixIndexProperty<T> : IndexProperty<T>
    {
        public abstract IndexFilter<T> Prefix(string prefix);
    }

    public abstract class IndexProperty<T>
    {
        public abstract IndexFilter<T> Equals<TValue>(TValue value);
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

        Task<IIndexQueryResult<T>> ExecuteAsync();
    }

    public interface IIndexQueryResult<T>
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
        /// The total number of results matching the query. 
        /// NOTE: This may be greater than the number of results returned.
        /// </summary>
        int HitCount { get; }

        /// <summary>
        /// The spent executing the query
        /// </summary>
        TimeSpan QueryTime { get; }

        /// <summary>
        /// The results of the query
        /// </summary>
        IReadOnlyList<T> Results { get; }
    }

    public interface IIndexQueryFilter<T>
    {
    }


}
