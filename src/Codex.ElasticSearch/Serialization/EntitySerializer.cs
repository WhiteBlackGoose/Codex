using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Nest;
using Codex.ObjectModel;
using System.IO;

namespace Codex.Serialization
{
    //class EntitySerializer : DefaultContractResolver
    //{
    //    protected override JsonContract CreateContract(Type objectType)
    //    {
    //        return base.CreateContract(objectType);
    //    }

    //    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    //    {
    //        var properties = base.CreateProperties(type, memberSerialization);

    //        foreach (var property in properties)
    //        {
    //        }

    //        return properties;
    //    }

    //    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    //    {
    //        return base.CreateProperty(member, memberSerialization);
    //    }

    //}

    public class JsonPrimitiveConverter<TType> : JsonConverter
    {
        private Action<JsonWriter, TType> Write { get; }
        private Func<JsonReader, TType> Read { get; }

        public JsonPrimitiveConverter(Action<JsonWriter, TType> write, Func<JsonReader, TType> read)
        {
            Write = write;
            Read = read;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return Read(reader);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Write(writer, (TType)value);
        }
    }

    public class EntityContractResolver : DefaultContractResolver
    {
        private Dictionary<Type, JsonConverter> m_primitives = new Dictionary<Type, JsonConverter>();

        public EntityContractResolver()
        {
            AddPrimitive(r => SymbolId.UnsafeCreateWithValue((string)r.Value), (w, id) => w.WriteValue(id.Value));
        }

        private void AddPrimitive<TType>(Func<JsonReader, TType> read, Action<JsonWriter, TType> write)
        {
            m_primitives[typeof(TType)] = new JsonPrimitiveConverter<TType>(write, read);
        }

        protected override JsonConverter ResolveContractConverter(Type objectType)
        {
            return base.ResolveContractConverter(objectType);
        }

        protected override JsonContract CreateContract(Type objectType)
        {
            if (m_primitives.TryGetValue(objectType, out var converter))
            {
                var contract = CreatePrimitiveContract(objectType);
                contract.Converter = converter;
                return contract;
            }

            return base.CreateContract(objectType);
        }

        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            var members = base.GetSerializableMembers(objectType);

            var serializationInterfaceAttribute = objectType.GetAttribute<SerializationInterfaceAttribute>();
            if (serializationInterfaceAttribute != null)
            {
                var serializationInterfaceType = serializationInterfaceAttribute.Type;

                var memberNames = new HashSet<string>(new[] { serializationInterfaceType }.Concat(serializationInterfaceType.GetInterfaces())
                    .SelectMany(i => i.GetProperties()).Select(p => p.Name));

                members.RemoveAll(m => !memberNames.Contains(m.Name));
            }

            return members;
        }
    }

    public static class EntityReflectionHelpers
    {
        public static string GetMetadataName(this Type type)
        {
            if (type.IsGenericType)
            {
                return type.GetGenericTypeDefinition().FullName;
            }

            return type.FullName;
        }

        public static bool GetInline(this MemberInfo type)
        {
            var attribute = type.GetAttribute<SearchDescriptorInlineAttribute>();
            return attribute?.Inline ?? true;
        }

        public static ObjectStage GetAllowedStages(this MemberInfo type)
        {
            var attribute = type.GetAttribute<IncludeAttribute>();
            return attribute?.AllowedStages ?? ObjectStage.All;
        }

        public static SearchBehavior? GetSearchBehavior(this MemberInfo type)
        {
            var attribute = type.GetAttribute<SearchBehaviorAttribute>();
            return attribute?.Behavior;
        }

        public static string Serialize(this JsonSerializer serializer, object entity)
        {
            Placeholder.Todo("Pool string writers");
            StringWriter writer = new StringWriter();

            serializer.Serialize(writer, entity);

            return writer.ToString();
        }

        public static T GetAttribute<T>(this MemberInfo type)
        {
            return type.GetCustomAttributes(typeof(T), inherit: false).OfType<T>().FirstOrDefault();
        }
    }


    //internal class CachingResolver : IContractResolver
    //{
    //    private IContractResolver m_inner;
    //    private ConcurrentDictionary<Type, JsonContract> ContractsByType
    //        = new ConcurrentDictionary<System.Type, JsonContract>();

    //    public CachingResolver(IContractResolver inner)
    //    {
    //        m_inner = inner;
    //    }

    //    public JsonContract ResolveContract(Type objectType)
    //    {
    //        JsonContract contract;
    //        if (!ContractsByType.TryGetValue(objectType, out contract))
    //        {
    //            contract = ContractsByType.GetOrAdd(objectType, m_inner.ResolveContract(objectType));
    //        }

    //        return contract;
    //    }
    //}
}
