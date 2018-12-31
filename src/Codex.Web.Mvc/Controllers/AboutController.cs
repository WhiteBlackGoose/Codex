using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Codex.ObjectModel;
using Codex.Sdk.Search;

namespace Codex.Web.Mvc.Controllers
{
    public class AboutController : Controller
    {
        private readonly ICodex Storage;

        public AboutController(ICodex storage)
        {
            Storage = storage;
        }

        [Route("about")]
        public async Task<ActionResult> About()
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