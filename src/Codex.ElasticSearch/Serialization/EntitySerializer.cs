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
using Codex.Utilities;
using Codex.Sdk.Utilities;

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

    public class CachingContractResolver : IContractResolver
    {
        private IContractResolver m_inner;
        private ConcurrentDictionary<Type, JsonContract> ContractsByType
            = new ConcurrentDictionary<System.Type, JsonContract>();

        public CachingContractResolver(IContractResolver inner)
        {
            m_inner = inner;
        }

        public JsonContract ResolveContract(Type objectType)
        {
            JsonContract contract;
            if (!ContractsByType.TryGetValue(objectType, out contract))
            {
                contract = ContractsByType.GetOrAdd(objectType, m_inner.ResolveContract(objectType));
            }

            return contract;
        }
    }

    public class CompositeEntityResolver : IContractResolver
    {
        private readonly EntityContractResolver entityContractResolver;
        private readonly IContractResolver fallContractResolver;

        public CompositeEntityResolver(EntityContractResolver entityContractResolver, IContractResolver fallContractResolver)
        {
            this.entityContractResolver = entityContractResolver;
            this.fallContractResolver = fallContractResolver;
        }

        public JsonContract ResolveContract(Type type)
        {
            if (CodexTypeUtilities.IsEntityType(type))
            {
                return entityContractResolver.ResolveContract(type);
            }
            else
            {
                return fallContractResolver.ResolveContract(type);
            }
        }
    }

    public class EntityContractResolver : DefaultContractResolver
    {
        private readonly ObjectStage stage;
        private readonly Dictionary<Type, JsonConverter> primitives = new Dictionary<Type, JsonConverter>();

        private readonly IComparer<MemberInfo> MemberComparer = new ComparerBuilder<MemberInfo>()
            .CompareByAfter(m => m.Name, StringComparer.Ordinal);

        public EntityContractResolver(ObjectStage stage)
        {
            this.stage = stage;
            AddPrimitive(r => SymbolId.UnsafeCreateWithValue((string)r.Value), (w, id) => w.WriteValue(id.Value));
        }

        private void AddPrimitive<TType>(Func<JsonReader, TType> read, Action<JsonWriter, TType> write)
        {
            primitives[typeof(TType)] = new JsonPrimitiveConverter<TType>(write, read);
        }

        protected override JsonConverter ResolveContractConverter(Type objectType)
        {
            return base.ResolveContractConverter(objectType);
        }

        protected override JsonContract CreateContract(Type objectType)
        {
            objectType = CodexTypeUtilities.GetImplementationType(objectType);

            if (primitives.TryGetValue(objectType, out var converter))
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

                Dictionary<string, MemberInfo> interfaceMemberMap = new Dictionary<string, MemberInfo>();
                foreach (var property in new[] { serializationInterfaceType }.Concat(serializationInterfaceType.GetInterfaces())
                    .SelectMany(i => i.GetProperties()))
                {
                    if (!interfaceMemberMap.ContainsKey(property.Name))
                    {
                        interfaceMemberMap.Add(property.Name, property);
                    }
                }

                members.RemoveAll(m =>
                {
                    if (interfaceMemberMap.TryGetValue(m.Name, out var interfaceProperty))
                    {
                        return (interfaceProperty.GetAllowedStages() & stage) == 0;
                    }

                    return true;
                });
            }

            members.Sort(MemberComparer);

            return members;
        }
    }

    public static class EntityReflectionHelpers
    {
        private static readonly JsonSerializer[] StageSerializers = GetSerializers();

        private static JsonSerializer[] GetSerializers()
        {
            var serializers = new JsonSerializer[(int)ObjectStage.All + 1];
            for (int stage = 0; stage <= (int)ObjectStage.All; stage++)
            {
                serializers[stage] = JsonSerializer.Create(new JsonSerializerSettings()
                {
                    ContractResolver = new CachingContractResolver(new EntityContractResolver((ObjectStage)stage)),
                    DefaultValueHandling = DefaultValueHandling.Ignore
                });
            }

            return serializers;
        }

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

        public static void PopulateContentIdAndSize<T>(this T entity)
            where T : class, ISearchEntity
        {
            if (entity.EntityContentId == null || entity.EntityContentSize == 0)
            {
                using (var lease = Pools.EncoderContextPool.Acquire())
                {
                    var encoderContext = lease.Instance;
                    entity.SerializeEntityTo(encoderContext.Writer, stage: ObjectStage.Index);
                    entity.EntityContentId = encoderContext.ToBase64HashString();
                    entity.EntityContentSize = encoderContext.StringBuilder.Length;

                    if (entity.Uid == null)
                    {
                        entity.Uid = entity.EntityContentId;
                    }
                }
            }
        }

        public static string SerializeEntity(this object entity, ObjectStage stage = ObjectStage.All)
        {
            return StageSerializers[(int)stage].Serialize(entity);
        }

        public static void SerializeEntityTo(this object entity, TextWriter writer, ObjectStage stage = ObjectStage.All)
        {
            StageSerializers[(int)stage].Serialize(writer, entity);
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
