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

    //class EntityResolver : ElasticContractResolver
    //{
    //    protected override string ResolvePropertyName(string fieldName)
    //    {
    //        return base.ResolvePropertyName(fieldName);
    //    }
    //}


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
