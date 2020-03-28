using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;

namespace Codex.Schema
{
    public class Schemas
    {
        public static Lazy<ISchemaDefinition<CodeReviewCommentThread, ICodeReviewCommentThread>> CodeReviewCommentThreadSchema { get; } 
            = new Lazy<ISchemaDefinition<CodeReviewCommentThread, ICodeReviewCommentThread>>(() =>
            {
                return new SchemaBuilder().Create<CodeReviewCommentThread, ICodeReviewCommentThread>(() => new CodeReviewCommentThread())
                    .Field<ILineSpan, LineSpan>(nameof(ICodeReviewCommentThread.OriginalSpan), (o, i) => o.OriginalSpan, (o, i, v) => o.OriginalSpan = v, () => LineSpanSchema.Value);
            });

        public static Lazy<ISchemaDefinition<ILineSpan>> LineSpanSchema { get; }
    }

    public interface ISchemaVisitor
    {
        void VisitField<TObject, TValue>(FieldDefinition<TObject, TValue> field, TObject o);
    }

    public class SchemaBuilder
    {
        public SchemaBuilder<TMutable, TImmutable> Create<TMutable, TImmutable>(Func<TMutable> create)
            where TMutable : TImmutable
        {
            return new SchemaBuilder<TMutable, TImmutable>(create);
        }
    }

    public class SchemaBuilder<TMutable, TImmutable> : ISchemaDefinition<TMutable, TImmutable>
        where TMutable : TImmutable
    {
        private readonly Func<TMutable> create;
        private readonly List<IFieldDefinition<TImmutable>> fields = new List<IFieldDefinition<TImmutable>>();

        public SchemaBuilder(Func<TMutable> create)
        {
            this.create = create;
        }

        public ISchemaDefinition<TImmutable> ImmutableSchema => throw new NotImplementedException();

        public ISchemaDefinition<TMutable> MutableSchema => throw new NotImplementedException();

        public SchemaBuilder<TMutable, TImmutable> Field<TValue, TMutableValue>(string name, Func<TImmutable, int, TValue> get, Action<TMutable, int, TMutableValue> set, Func<ISchemaDefinition<TValue>> getSchema)
        {
            throw new NotImplementedException();
        }
    }

    public interface ISchemaDefinition<TObject>
    {
        TObject New();

        void Visit(ISchemaVisitor visitor, TObject o);
    }

    public interface ISchemaDefinition<TMutable, TImmutable>
        where TMutable : TImmutable
    {
        ISchemaDefinition<TImmutable> ImmutableSchema { get; }

        ISchemaDefinition<TMutable> MutableSchema { get; }
    }

    public interface IFieldDefinition
    {
        string Name { get; }
        bool IsArray { get; }
    }

    public interface IFieldDefinition<TObject> : IFieldDefinition
    {
        ISchemaDefinition<TObject> Schema { get; }

        void Visit(ISchemaVisitor visitor, TObject o);
    }

    public interface IFieldDefinition<TObject, TValue> : IFieldDefinition<TObject>
    {
        int GetLength(TObject o);
        TValue Get(TObject o, int index = 0);
        void Set(TObject o, TValue value, int index = 0);
    }

    public class FieldDefinition<TObject, TValue> : IFieldDefinition<TObject, TValue>
    {
        private readonly Func<TObject, int, TValue> get;
        private readonly Action<TObject, int, TValue> set;
        private readonly Func<ISchemaDefinition<TValue>> getSchema;

        public FieldDefinition(string name, Func<TObject, int, TValue> get, Action<TObject, int, TValue> set, Func<ISchemaDefinition<TValue>> getSchema)
        {

        }

        public ISchemaDefinition<TObject> Schema => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public bool IsArray => throw new NotImplementedException();

        public TValue Get(TObject o, int index = 0)
        {
            throw new NotImplementedException();
        }

        public int GetLength(TObject o)
        {
            throw new NotImplementedException();
        }

        public void Set(TObject o, TValue value, int index = 0)
        {
            throw new NotImplementedException();
        }

        public void Visit(ISchemaVisitor visitor, TObject o)
        {
            visitor.VisitField(this, o);
        }
    }

    //public abstract class SchemaDefinition<TObject> : SchemaDefinition
    //{
    //    public IReadOnlyList<FieldDefinition<TObject>> Fields { get; }

    //    public abstract TObject New();

    //    public void Visit(ISchemaVisitor visitor, TObject o)
    //    {
    //        foreach (var field in Fields)
    //        {
    //            field.Visit(visitor, o);
    //        }
    //    }
    //}

    //public abstract class FieldDefinition
    //{
    //    public abstract string Name { get; }
    //    public abstract bool IsArray { get; }
    //}

    //public abstract class FieldDefinition<TObject> : FieldDefinition
    //{
    //    public abstract void Visit(ISchemaVisitor visitor, TObject o);
    //}

    //public abstract class FieldDefinition<TObject, TValue> : FieldDefinition<TObject>
    //{
    //    public abstract SchemaDefinition<TValue> Schema { get; }
    //    public abstract int GetLength(TObject o);
    //    public abstract TValue Get(TObject o, int? index = null);
    //    public abstract void Set(TObject o, TValue value, int? index = null);

    //    public override void Visit(ISchemaVisitor visitor, TObject o)
    //    {
    //        visitor.VisitField(this, o);
    //    }
    //}

}
