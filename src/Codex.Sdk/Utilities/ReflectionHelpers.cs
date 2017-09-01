using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Sdk.Utilities
{
    public static class ReflectionHelpers
    {
        public static DataInclusionOptions? GetDataInclusion(this MemberInfo type)
        {
            var attribute = type.GetAttribute<DataInclusionAttribute>();
            return attribute?.DataInclusion;
        }

        public static SearchBehavior? GetSearchBehavior(this MemberInfo type)
        {
            var attribute = type.GetAttribute<SearchBehaviorAttribute>();
            return attribute?.Behavior;
        }

        public static T GetAttribute<T>(this MemberInfo type)
        {
            return type.GetCustomAttributes(typeof(T), inherit: false).OfType<T>().FirstOrDefault();
        }
    }
}
