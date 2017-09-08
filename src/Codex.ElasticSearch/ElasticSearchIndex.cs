using Codex.Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    class ElasticSearchIndex : Index
    {
        /// <summary>
        /// Creates an elasticsearch store with the given prefix for indices
        /// </summary>
        public ElasticSearchIndex(ElasticSearchStoreConfiguration configuration)
        {

        }

        //public override IndexQuery<T> CreateQuery<T>()
        //{
        //    throw new NotImplementedException();
        //}

        private class ElasticSearchProperty<T>
        {

        }
    }
}