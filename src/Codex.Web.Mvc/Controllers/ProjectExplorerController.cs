using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Web.Mvc.Rendering;

namespace Codex.Web.Mvc.Controllers
{
    public class ProjectExplorerController : Controller
    {
        private readonly ICodex Storage;

        public ProjectExplorerController(ICodex storage)
        {
            Storage = storage;
        }

        [Route("projectexplorer/{projectId}")]
        [Route("repos/{repoName}/projectexplorer/{projectId}")]
        public async Task<ActionResult> ProjectExplorer(string projectId)
        {
            try
            {
                Requests.LogRequest(this);
                var getProjectResponse = await Storage.GetProjectAsync(new GetProjectArguments()
                {
                    RepositoryScopeId = this.GetSearchRepo(),
                    ProjectId = projectId,
                });

                var renderer = new ProjectExplorerRenderer(getProjectResponse.ThrowOnError().Result);
                var text = renderer.GenerateProjectExplorer();

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