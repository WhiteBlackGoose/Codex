using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Codex.ElasticSearch
{
    /// <summary>
    /// Function used to merge values for update
    /// </summary>
    public delegate T UpdateMergeFunction<T>(T oldValue, T newValue);
}