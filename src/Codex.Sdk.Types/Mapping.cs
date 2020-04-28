using System.Collections.Generic;

namespace Codex.ObjectModel
{
    public class MappingInfo
    {
        public string Name { get; }
        public string FullName { get; }
        public SearchBehavior? SearchBehavior { get; }

        public MappingInfo(string name, MappingInfo parent, SearchBehavior? searchBehavior)
        {
            Name = name;
            FullName = parent == null
                ? Name
                : string.Join(".", parent.Name, Name);
        }
    }

    public class MappingBase
    {
        public MappingInfo MappingInfo { get; }

        public MappingBase(MappingInfo info)
        {
            MappingInfo = info;
        }

        public virtual IEnumerable<MappingBase> Children { get; }
    }

    public interface IMapping<T>
    {
        void Visit(IVisitor visitor, T value);
    }

    public partial class Mappings
    {
        public MappingInfo MappingInfo { get; }
    }
}