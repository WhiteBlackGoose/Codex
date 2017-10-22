using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Utilities
{
    public class IdMapper<T> : IdMapper
        where T : ISearchEntity
    {
        public ConnectionSettings MapId(ConnectionSettings settings)
        {
            return settings.MapIdPropertyFor<T>(entity => entity.Uid);
        }
    }

    public interface IdMapper
    {
        ConnectionSettings MapId(ConnectionSettings settings);
    }
}
