using Codex.ObjectModel;
using Codex.Storage.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Utilities
{
    public class ElasticCodexTypeUtilities : CodexTypeUtilities
    {
        public static readonly CodexTypeUtilities Instance = new ElasticCodexTypeUtilities();

        private IReadOnlyDictionary<Type, Type> s_typeMappings = CreateTypeMappings();

        private static IReadOnlyDictionary<Type, Type> CreateTypeMappings()
        {
            Dictionary<Type, Type> typeMappings = new Dictionary<Type, Type>();
            MapType<IReferenceList, ReferenceListModel>(typeMappings);
            return typeMappings;
        }

        private static void MapType<TInterface, TImplemenation>(Dictionary<Type, Type> typeMappings)
            where TImplemenation : class, TInterface
        {
            typeMappings[typeof(TInterface)] = typeof(TImplemenation);
            typeMappings[typeof(TImplemenation)] = typeof(TInterface);
        }

        protected override bool TryGetMappedType(Type type, out Type mappedType)
        {
            return s_typeMappings.TryGetValue(type, out mappedType) || base.TryGetMappedType(type, out mappedType);
        }
    }
}
