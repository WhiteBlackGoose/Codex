using Codex.ObjectModel;
using Codex.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Tests
{
    static class TestExtensions
    {
        public static Task AddDefinitions(this ElasticSearchEntityStore<IDefinitionSearchModel> store, params DefinitionSymbol[] definitions)
        {
            return store.AddAsync(definitions.Select(d => new DefinitionSearchModel() { Definition = d }.PopulateContentIdAndSize()).ToList());
        }
    }
}
