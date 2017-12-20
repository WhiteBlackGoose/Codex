using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Codex.Sdk.Search;

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

        [ServiceMethod(CodexServiceMethod.Search)]
        public async Task<IndexQueryHitsResponse<ISearchResult>> SearchAsync(SearchArguments arguments)
        {
            var result = await Codex.SearchAsync(arguments);
            return result;
        }

        [ServiceMethod(CodexServiceMethod.FindAllRefs)]
        public Task<IndexQueryResponse<ReferencesResult>> FindAllReferencesAsync([FromBody]FindAllReferencesArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments)
        {
            throw new NotImplementedException();
        }

        [ServiceMethod(CodexServiceMethod.FindDefLocation)]
        public async Task<IndexQueryResponse<ReferencesResult>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments)
        {
            var result = await Codex.FindDefinitionLocationAsync(arguments);
            return result;
        }

        [ServiceMethod(CodexServiceMethod.GetSource)]
        public async Task<IndexQueryResponse<IBoundSourceFile>> GetSourceAsync(GetSourceArguments arguments)
        {
            var result = await Codex.GetSourceAsync(arguments);
            return result;
        }
    }
}
