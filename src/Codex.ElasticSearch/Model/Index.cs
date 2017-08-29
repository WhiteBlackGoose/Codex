using Codex.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Sdk.Search
{
    public class IndexCodex
    {
        public Task FindDefinitionAsync(ReferenceSpan reference)
        {
            var filter = 
                DefinitionSearchModelSearchDescriptor.SymbolId.Match(reference.Reference.Id.Value) |
                DefinitionSearchModelSearchDescriptor.ProjectId.Match(reference.Reference.ProjectId);
            //modelTerms.Definition.ProjectId.AsTerm<IDefinitionSearchModel>().Equals<string>(reference.Symbol.ProjectId);

            throw new NotImplementedException();
            //return query.ExecuteAsync();
        }
    }

    public abstract class PrefixIndexProperty<T> : IndexProperty<T>
    {
        public abstract IndexFilter<T> Prefix(string prefix);
    }

    public abstract class PrefixFullNameIndexProperty<T> : IndexProperty<T>
    {
    }

    public abstract class FullTextIndexProperty<T> : IndexProperty<T>
    {
    }

    public abstract class NormalizedKeywordIndexProperty<T> : IndexProperty<T>
    {
    }

    public abstract class IndexProperty<T>
    {
        public abstract IndexFilter<T> Match<TValue>(TValue value);
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
}
