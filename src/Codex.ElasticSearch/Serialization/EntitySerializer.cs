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
using Nest.JsonNetSerializer;
using Elasticsearch.Net;
using Codex.ElasticSearch;

namespace Codex.Serialization
{
    public class EntityJsonNetSerializer : JsonNetSerializer
    {
        public EntityJsonNetSerializer(
            IElasticsearchSerializer builtinSerializer, 
            IConnectionSettingsValues connectionSettings) 
            : base(builtinSerializer, connectionSettings)
        {
        }

        protected override IContractResolver CreateContractResolver()
        {
            return new CachingContractResolver(new EntityContractResolver(ObjectStage.Search, this.ConnectionSettings));
        }
    }

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
        private object m_contractsLock = new object();

        public CachingContractResolver(IContractResolver inner)
        {
            m_inner = inner;
        }

        public JsonContract ResolveContract(Type objectType)
        {
            JsonContract contract;
            if (!ContractsByType.TryGetValue(objectType, out contract))
            {
                lock (m_contractsLock)
                {
                    contract = ContractsByType.GetOrAdd(objectType, k =>
                    {
                        var innerContract = m_inner.ResolveContract(objectType);
                        lock (innerContract)
                        {
                            var objectContract = innerContract as JsonObjectContract;
                            if (objectContract != null)
                            {
                                var sortedProperties = objectContract.Properties.ToList();
                                sortedProperties.Sort((p1, p2) => StringComparer.OrdinalIgnoreCase.Compare(p1.PropertyName, p2.PropertyName));
                                objectContract.Properties.Clear();
                                foreach (var property in sortedProperties)
                                {
                                    objectContract.Properties.Add(property);
                                }
                            }

                            return innerContract;
                        }
                    });
                }
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

    public class EntityContractResolver : ConnectionSettingsAwareContractResolver
    {
        private readonly ObjectStage stage;
        private readonly Dictionary<Type, JsonConverter> primitives = new Dictionary<Type, JsonConverter>();

        private readonly Action<JsonContract, bool> isSealedFieldSetter = EntityReflectionHelpers.CreateFieldSetter<JsonContract, bool>("IsSealed");
        private readonly Func<JsonContract, bool> isSealedFieldGetter = EntityReflectionHelpers.CreateFieldGetter<JsonContract, bool>("IsSealed");
        private readonly Action<JsonContainerContract, JsonContract> finalItemContractFieldSetter = EntityReflectionHelpers.CreateFieldSetter<JsonContainerContract, JsonContract>("_finalItemContract");
        private readonly Action<JsonContainerContract, JsonContract> itemContractFieldSetter = EntityReflectionHelpers.CreateFieldSetter<JsonContainerContract, JsonContract>("_itemContract");

        private readonly IComparer<MemberInfo> MemberComparer = new ComparerBuilder<MemberInfo>()
            .CompareByAfter(m => m.Name, StringComparer.Ordinal);

        public EntityContractResolver(ObjectStage stage, IConnectionSettingsValues settings = null)
            : base (settings ?? new ConnectionSettings())
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

        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
        {
            var contract = base.CreateDictionaryContract(objectType);
            contract.DictionaryKeyResolver = propertyName => propertyName;
            return contract;
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
                // actual member. Also, need to set as sealed.
                itemContractFieldSetter(arrayContract, itemContract);
                finalItemContractFieldSetter(arrayContract, itemContract);
                isSealedFieldSetter(arrayContract, true);
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

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var properties = base.CreateProperties(type, memberSerialization).AsList();

            var serializationInterfaceAttribute = type.GetAttribute<SerializationInterfaceAttribute>();
            if (serializationInterfaceAttribute != null)
            {
                var excludedSerializationProperties = new HashSet<string>(type.GetCustomAttributes<ExcludedSerializationPropertyAttribute>()
                    .Select(attr => attr.PropertyName));

                var serializationInterfaceType = serializationInterfaceAttribute.Type;

                Dictionary<string, MemberInfo> interfaceMemberMap = new Dictionary<string, MemberInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in new[] { serializationInterfaceType }.Concat(serializationInterfaceType.GetInterfaces())
                    .SelectMany(i => i.GetProperties()))
                {
                    if (!interfaceMemberMap.ContainsKey(property.Name))
                    {
                        interfaceMemberMap.Add(property.Name, property);
                    }
                }

                properties.RemoveAll(property =>
                {
                    if (excludedSerializationProperties.Contains(property.PropertyName))
                    {
                        return true;
                    }

                    if (interfaceMemberMap.TryGetValue(property.PropertyName, out var interfaceProperty))
                    {
                        return (interfaceProperty.GetAllowedStages() & stage) == 0;
                    }

                    return true;
                });
            }

            return properties;
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
    }

    public static class EntityReflectionHelpers
    {
        private static readonly JsonSerializer[] StageSerializers = GetSerializers();

        private static JsonSerializer[] GetSerializers()
        {
            var maxStage = Enum.GetValues(typeof(ObjectStage)).OfType<ObjectStage>().Max();
            var serializers = new JsonSerializer[(int)maxStage + 1];
            for (int stage = 0; stage < serializers.Length; stage++)
            {
                serializers[stage] = JsonSerializer.Create(new JsonSerializerSettings()
                {
                    ContractResolver = new CachingContractResolver(new EntityContractResolver((ObjectStage)stage)),
                    DefaultValueHandling = DefaultValueHandling.Ignore
                });
            }

            return serializers;
        }

        public static Func<TArg, TResult> CreateStaticMethodCall<TArg, TResult>(Type type, string methodName)
        {
            var arg0 = Expression.Parameter(typeof(TArg), "arg0");
            return Expression.Lambda<Func<TArg, TResult>>(
                Expression.Call(
                    type,
                    methodName,
                    typeArguments: null,
                    arguments: new Expression[]
                    {
                        arg0
                    }),
                arg0).Compile();
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

        public static T PopulateContentIdAndSize<T>(this T entity, bool force = false)
            where T : class, ISearchEntity
        {
            if (entity.EntityContentId == null || entity.EntityContentSize == 0 || force)
            {
                using (var lease = Pools.EncoderContextPool.Acquire())
                {
                    // TODO: These fields and routing and stable id should be excluded from serialization when hashing
                    entity.EntityContentId = null;
                    entity.EntityContentSize = 0;

                    var encoderContext = lease.Instance;
                    entity.SerializeEntityTo(encoderContext.Writer, stage: ObjectStage.Index);
                    entity.EntityContentId = encoderContext.ToBase64HashString();
                    entity.EntityContentSize = encoderContext.StringBuilder.Length;
                    entity.RoutingGroup = entity.GetStableIdGroup();

                    if (entity.Uid == null || force)
                    {
                        entity.Uid = entity.EntityContentId + entity.GetRoutingSuffix();
                    }
                }
            }

            return entity;
        }

        public static string GetEntityContentId(this EntityBase entity, ObjectStage stage = ObjectStage.All)
        {
            using (var lease = Pools.EncoderContextPool.Acquire())
            {
                var encoderContext = lease.Instance;
                entity.SerializeEntityTo(encoderContext.Writer, stage: ObjectStage.Index);
                return encoderContext.ToBase64HashString();
            }
        }

        public static T DeserializeEntity<T>(this TextReader reader, ObjectStage stage = ObjectStage.All)
        {
            return StageSerializers[(int)stage].Deserialize<T>(new JsonTextReader(reader));
        }

        public static string SerializeEntity(this object entity, ObjectStage stage = ObjectStage.All)
        {
            return StageSerializers[(int)stage].Serialize(entity);
        }

        public static void SerializeEntityTo(this object entity, TextWriter writer, ObjectStage stage = ObjectStage.All)
        {
            StageSerializers[(int)stage].Serialize(writer, entity);
        }

        public static void SerializeEntityTo(this object entity, JsonWriter writer, ObjectStage stage = ObjectStage.All)
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
}
