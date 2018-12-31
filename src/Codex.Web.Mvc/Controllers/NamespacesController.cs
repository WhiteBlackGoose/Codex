using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Web.Mvc.Rendering;

namespace Codex.Web.Mvc.Controllers
{
    public class NamespacesController : Controller
    {
        private readonly ICodex Storage;

        public NamespacesController(ICodex storage)
        {
            Storage = storage;
        }

        [Route("namespaces/{projectId}")]
        public async Task<ActionResult> Namespaces(string projectId)
        {
            try
            {
                Requests.LogRequest(this);
                var renderer = new NamespacesRenderer(Storage, projectId);
                var text = renderer.Generate();

                Responses.PrepareResponse(Response);

                return PartialView((object)text);
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }
    }
}