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
using System.Collections;
using System.Linq.Expressions;
using Codex.Storage.DataModel;
using Codex.ElasticSearch.Utilities;

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

    public class CachingElasticContractResolver : ElasticContractResolver
    {
        private IContractResolver m_inner;

        public CachingElasticContractResolver(IContractResolver inner, IConnectionSettingsValues connectionSettings = null, IList<Func<Type, JsonConverter>> contractConverters = null)
            : base (connectionSettings ?? new ConnectionSettings(), contractConverters)
        {
            m_inner = new CachingContractResolver(inner);
        }

        public override JsonContract ResolveContract(Type objectType)
        {
            return m_inner.ResolveContract(objectType);
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
            if (entityContractResolver.HandlesType(type))
            {
                return entityContractResolver.ResolveContract(type);
            }
            else
            {
                var contract = fallContractResolver.ResolveContract(type);
                var arrayContract = contract as JsonArrayContract;
                if (arrayContract?.CollectionItemType != null &&
                    entityContractResolver.HandlesType(arrayContract.CollectionItemType))
                {
                    return entityContractResolver.ResolveContract(type);
                }

                return contract;
            }
        }
    }

    public class EntityContractResolver : ElasticContractResolver
    {
        private readonly ObjectStage stage;
        private readonly Dictionary<Type, JsonConverter> primitives = new Dictionary<Type, JsonConverter>();

        private readonly Action<JsonContract, bool> isSealedFieldSetter = EntityReflectionHelpers.CreateFieldSetter<JsonContract, bool>("IsSealed");
        private readonly Func<JsonContract, bool> isSealedFieldGetter = EntityReflectionHelpers.CreateFieldGetter<JsonContract, bool>("IsSealed");
        private readonly Action<JsonContainerContract, JsonContract> finalItemContractFieldSetter = EntityReflectionHelpers.CreateFieldSetter<JsonContainerContract, JsonContract>("_finalItemContract");
        private readonly Action<JsonContainerContract, JsonContract> itemContractFieldSetter = EntityReflectionHelpers.CreateFieldSetter<JsonContainerContract, JsonContract>("_itemContract");

        private readonly IComparer<MemberInfo> MemberComparer = new ComparerBuilder<MemberInfo>()
            .CompareByAfter(m => m.Name, StringComparer.Ordinal);

        public EntityContractResolver(ObjectStage stage)
            : base (new ConnectionSettings(), null)
        {
            this.stage = stage;
            AddPrimitive(r => SymbolId.UnsafeCreateWithValue((string)r.Value), (w, id) => w.WriteValue(id.Value));
        }

        private void AddPrimitive<TType>(Func<JsonReader, TType> read, Action<JsonWriter, TType> write)
        {
            primitives[typeof(TType)] = new JsonPrimitiveConverter<TType>(write, read);
        }

        internal bool HandlesType(Type objectType)
        {
            return primitives.ContainsKey(objectType) || ElasticCodexTypeUtilities.Instance.IsEntityType(objectType);
        }

        protected override JsonConverter ResolveContractConverter(Type objectType)
        {
            var result = base.ResolveContractConverter(objectType);
            return result;
        }

        protected override JsonObjectContract CreateObjectContract(Type objectType)
        {
            return base.CreateObjectContract(objectType);
        }

        protected override JsonArrayContract CreateArrayContract(Type objectType)
        {
            var arrayContract = base.CreateArrayContract(objectType);
            if (HandlesType(arrayContract.CollectionItemType))
            {
                var itemContract = CreateContract(arrayContract.CollectionItemType);

                // Set the item contract and final item contract, so collection items
                // are serialized based on collection item type rather than type of 
                // actual member
                itemContractFieldSetter(arrayContract, itemContract);
                finalItemContractFieldSetter(arrayContract, itemContract);
            }

            return arrayContract;
        }

        protected override JsonContract CreateContract(Type objectType)
        {
            objectType = ElasticCodexTypeUtilities.Instance.GetImplementationType(objectType);

            if (primitives.TryGetValue(objectType, out var converter))
            {
                var primitiveContract = CreatePrimitiveContract(objectType);
                primitiveContract.Converter = converter;
                return primitiveContract;
            }

            var contract = base.CreateContract(objectType);

            if (contract != null && ElasticCodexTypeUtilities.Instance.IsEntityType(objectType))
            {
                if (!isSealedFieldGetter(contract))
                {
                    // Set JsonContract.IsSealed=true, so members are serialized via their property type (not the type
                    // of the actual object)
                    isSealedFieldSetter(contract, true);
                    contract.OnSerializingCallbacks.Add((obj, context) =>
                    {
                        (obj as ISerializableEntity)?.OnSerializing();
                    });

                    contract.OnDeserializedCallbacks.Add((obj, context) =>
                    {
                        (obj as ISerializableEntity)?.OnDeserialized();
                    });
                }
            }

            return contract;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (property != null)
            {
                property.DefaultValueHandling = DefaultValueHandling.Ignore;

                if (property.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                {
                    property.ShouldSerialize = instance =>
                    {
                        IEnumerable enumerable = null;

                        // this value could be in a public field or public property
                        switch (member.MemberType)
                        {
                            case MemberTypes.Property:
                                enumerable = instance
                                    .GetType()
                                    .GetProperty(member.Name)
                                    .GetValue(instance, null) as IEnumerable;
                                break;
                            case MemberTypes.Field:
                                enumerable = instance
                                    .GetType()
                                    .GetField(member.Name)
                                    .GetValue(instance) as IEnumerable;
                                break;
                            default:
                                break;

                        }

                        if (enumerable == null || (enumerable as ICollection)?.Count == 0)
                        {
                            return false;
                        }
                        else
                        {
                            // check to see if there is at least one item in the Enumerable
                            return enumerable.GetEnumerator().MoveNext();
                        }
                    };
                }
            }

            return property;
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
                    ContractResolver = new CachingElasticContractResolver(new EntityContractResolver((ObjectStage)stage)),
                    DefaultValueHandling = DefaultValueHandling.Ignore
                });
            }

            return serializers;
        }

        public static Action<TType, TFieldType> CreateFieldSetter<TType, TFieldType>(string fieldName)
        {
            var objectParameter = Expression.Parameter(typeof(TType), "obj");
            var fieldValueParameter = Expression.Parameter(typeof(TFieldType), "value");
            return Expression.Lambda< Action<TType, TFieldType>>(
                Expression.Assign(
                    Expression.Field(objectParameter, fieldName), 
                    fieldValueParameter), 
                objectParameter, 
                fieldValueParameter).Compile();
        }

        public static Func<TType, TFieldType> CreateFieldGetter<TType, TFieldType>(string fieldName)
        {
            var objectParameter = Expression.Parameter(typeof(TType), "obj");
            return Expression.Lambda<Func<TType, TFieldType>>(Expression.Field(objectParameter, fieldName), objectParameter).Compile();
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
