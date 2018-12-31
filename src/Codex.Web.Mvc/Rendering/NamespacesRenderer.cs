using Codex.ObjectModel;
using Codex.Sdk.Search;

namespace Codex.Web.Mvc.Rendering
{
    public class NamespacesRenderer
    {
        private string projectId;
        private ICodex storage;

        public NamespacesRenderer(ICodex storage, string projectId)
        {
            this.storage = storage;
            this.projectId = projectId;
        }

        public string Generate()
        {
            return "Namespaces";
        }
    }
}