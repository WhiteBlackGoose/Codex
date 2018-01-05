using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Codex.ObjectModel;
using Codex.Sdk.Search;

namespace WebUI.Controllers
{
    public class OverviewController : Controller
    {
        private readonly ICodex Storage;

        public OverviewController(ICodex storage)
        {
            Storage = storage;
        }

        [Route("overview")]
        public async Task<ActionResult> Overview()
        {
            try
            {
                Requests.LogRequest(this);
                Responses.PrepareResponse(Response);

                return PartialView();
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }
    }
}