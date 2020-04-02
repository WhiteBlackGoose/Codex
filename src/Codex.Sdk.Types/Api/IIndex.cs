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

    public partial interface IIndex<T>
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

        public abstract Task<IndexQueryHitsResponse<T>> ExecuteAsync();
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

    // TODO: Sortword is normally also a normalized keyword. Is this always the case?
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

        Task<IndexQueryResponse<T>> ExecuteAsync();

        void Exclude();

    }

    public interface IIndexQueryFilter<T>
    {
        void Exclude();
    }
}
