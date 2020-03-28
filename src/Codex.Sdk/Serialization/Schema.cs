using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Schema
{
    public class SchemaDefinition
    {

    }

    public class SchemaDefinition<TObject> : SchemaDefinition
    {
        public IReadOnlyList<FieldDefinition<TObject>> Fields { get; }
    }

    public class FieldDefinition<TObject>
    {
        public string Name { get; }
    }

    public abstract class FieldDefinition<TObject, TValue> : FieldDefinition<TObject>
    {
        public abstract TValue Get(TObject o);
        public abstract void Set(TObject o, TValue value);
    }

    public abstract class BoundSchemaDefinition<TObject>
    {
        public SchemaDefinition<TObject> SerializedSchema { get; }
        public SchemaDefinition<TObject> Schema { get; }

        public IReadOnlyList<BoundFieldDefinition<TObject>> Fields { get; }

        public BoundSchemaDefinition(SchemaDefinition<TObject> serializedSchema, SchemaDefinition<TObject> schema)
        {
        }

        public abstract TObject Create();

        public TObject Deserialize(ObjectReader reader)
        {
            // TODO: Bit field for non-default fields
            var value = Create();
            foreach (var field in Fields)
            {
                field.Deserialize(reader, value);
            }

            return value;
        }

        public void Serialize(ObjectWriter writer, TObject value)
        {
            // TODO: Bit field for non-default fields
            foreach (var field in Fields)
            {
                field.Serialize(writer, value);
            }
        }
    }

    public abstract class BoundFieldDefinition<TObject>
    {
        public abstract void Deserialize(ObjectReader reader, TObject target);

        public abstract void Serialize(ObjectWriter writer, TObject target);
    }

    public abstract class BoundFieldDefinition<TObject, TValue> : BoundFieldDefinition<TObject>
    {
        private FieldDefinition<TObject, TValue> Definition { get; }

        private IValueSerializer<TValue> Serializer;
        private Action<TObject, TValue> Set;
        private Func<TObject, TValue> Get;

        public BoundFieldDefinition()
        {

        }

        public override void Deserialize(ObjectReader reader, TObject target)
        {
            if (Serializer.Deserialize(reader, out var value))
            {
                Set(target, value);
            }
        }

        public override void Serialize(ObjectWriter writer, TObject target)
        {
            var value = Get(target);
            Serializer.Serialize(writer, value);
        }
    }

    public class ObjectReader 
    {
    }

    public class ObjectWriter
    {
    }

    public interface IValueSerializer<TValue>
    {
        bool Deserialize(ObjectReader reader, out TValue value);

        void Serialize(ObjectWriter writer, TValue value);
    }

    public abstract class ValueSerializer<TValue>
    {
        public abstract TValue Deserialize(ObjectReader reader);

        public abstract void Serialize(ObjectWriter writer, TValue value);
    }
}
