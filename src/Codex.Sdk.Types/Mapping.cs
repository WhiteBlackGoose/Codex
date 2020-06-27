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

        public virtual MappingBase this[string fullName] => fullName == MappingInfo.FullName ? this : null;

        public bool IsMatch(string fullName, string childName)
        {
            var childStartIndex = MappingInfo.FullName.Length + 1;
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

    public interface IMapping<T>
    {
        void Visit(IVisitor visitor, T value);
    }

    public partial class Mappings : MappingBase
    {
        private MappingBase[] _mappingsBySearchType = new MappingBase[SearchTypes.RegisteredSearchTypes.Count];

        public MappingBase this[SearchType searchType]
        {
            get
            {
                var result = _mappingsBySearchType[searchType.Id];
                if (result == null)
                {
                    result = this[searchType.Name];
                    _mappingsBySearchType[searchType.Id] = result;
                }

                return result;
            }
        }

        public Mappings() 
            : base(info: null)
        {
        }
    }
}