using System.Collections.Generic;
using Codex.ObjectModel;
using Codex.Utilities;

namespace Codex.ObjectModel
{
    public class MappingInfo
    {
        public string Name { get; }
        public string FullName { get; }
        public SearchBehavior? SearchBehavior { get; }
        public ObjectStage ObjectStage { get; }

        public MappingInfo(string name, MappingInfo parent, SearchBehavior? searchBehavior, ObjectStage objectStage = ObjectStage.All)
        {
            Name = name;
            FullName = parent == null
                ? Name
                : string.Join(".", parent.Name, Name);
            SearchBehavior = searchBehavior;
            ObjectStage = objectStage;
        }
    }

    public class MappingBase : IMapping
    {
        public MappingInfo MappingInfo { get; }

        public MappingBase(MappingInfo info)
        {
            MappingInfo = info;
        }

        public virtual MappingBase this[string fullName] => fullName == MappingInfo.FullName ? this : null;

        public bool IsMatch(string fullName, string childName)
        {
            var childStartIndex = MappingInfo?.Name == null ? 0 : MappingInfo.FullName.Length + 1;
            var childFullNameLength = childStartIndex + childName.Length;

            if (childFullNameLength <= fullName.Length 
                && fullName.IndexOf(childName, childStartIndex, childName.Length) == childStartIndex)
            {
                return true;
            }

            return false;
        }

        public virtual IEnumerable<MappingBase> Children { get; }
    }

    public interface IMapping
    {
        MappingInfo MappingInfo { get; }
    }

    public interface IMapping<T> : IMapping
    {
        void Visit(IVisitor visitor, T value);
    }

    public interface IValueMapping<T> : IMapping
    {
        void Visit(IValueVisitor<T> visitor, T value);

        void Visit(IValueVisitor<T> visitor, IReadOnlyList<T> value);
    }

    public partial class Mappings : MappingBase
    {
        private LazySearchTypesMap<MappingBase> _mappingsByType;

        public MappingBase this[SearchType searchType] => _mappingsByType[searchType];

        public Mappings() 
            : base(info: null)
        {
            _mappingsByType = new LazySearchTypesMap<MappingBase>(s => this[s.Name]);
        }
    }
}