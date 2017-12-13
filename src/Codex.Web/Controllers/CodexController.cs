using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Codex.Sdk.Search;
using Codex.ObjectModel;

namespace Codex.Web.Controllers
{
    [Route("api/codex")]
    public class CodexController : Controller, ICodex
    {
        private ICodex Codex { get; set; }
        public CodexController(ICodex codex)
        {
            Codex = codex;
        }

        // GET api/values
        [HttpGet]
        public async Task<IEnumerable<string>> Get()
        {
            return new string[] { "hello", "world!" };
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]string value)
        {
        }

        [HttpPost(nameof(CodexServiceMethod.Search))]
        public async Task<IndexQueryHitsResponse<ISearchResult>> SearchAsync([FromBody]SearchArguments arguments)
        {
            var result = await Codex.SearchAsync(arguments);
            return result;
        }

        public Task<IndexQueryHitsResponse<IReferenceSearchModel>> FindAllReferencesAsync([FromBody]FindAllReferencesArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryHitsResponse<IReferenceSearchModel>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryResponse<IBoundSourceSearchModel>> GetSourceAsync(GetSourceArguments arguments)
        {
            throw new NotImplementedException();
        }
    }
}
