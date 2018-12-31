using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Web.Mvc.Rendering;
using Codex.Web.Mvc.Models;

namespace Codex.Web.Mvc.Controllers
{
    public class DocumentOutlineController : Controller
    {
        private readonly ICodex Storage;

        public DocumentOutlineController(ICodex storage)
        {
            Storage = storage;
        }

        [Route("documentoutline/{projectId}")]
        public async Task<ActionResult> DocumentOutline(string projectId, string filePath)
        {
            try
            {
                Requests.LogRequest(this);
                var getSourceResponse = await Storage.GetSourceAsync(new GetSourceArguments()
                {
                    ProjectId = projectId,
                    ProjectRelativePath = filePath,
                    DefinitionOutline = true
                });

                var boundSourceFile = getSourceResponse.ThrowOnError().Result;

                if (boundSourceFile == null)
                {
                    return PartialView("~/Views/DocumentOutline/DocumentOutline.cshtml", new EditorModel { Error = $"Bound source file for {filePath} in {projectId} not found." });
                }

                var renderer = new DocumentOutlineRenderer(projectId, boundSourceFile);
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