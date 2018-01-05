using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using WebUI.Rendering;

namespace WebUI.Controllers
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