using Bridge.Html5;
using Codex.Sdk.Search;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Retyped.jquery;

namespace Codex.View.Web
{
    public class WebApiCodex : ICodex
    {
        private readonly string baseUrl;

        public WebApiCodex(string baseUrl)
        {
            this.baseUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + '/';
        }

        public Task<IndexQueryHitsResponse<IReferenceSearchModel>> FindAllReferencesAsync(FindAllReferencesArguments arguments)
        {
            return PostAsync(CodexServiceMethod.FindAllRefs, arguments, c => c.FindAllReferencesAsync(arguments));
        }

        public Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments)
        {
            return PostAsync(CodexServiceMethod.FindDef, arguments, c => c.FindDefinitionAsync(arguments));
        }

        public Task<IndexQueryHitsResponse<IReferenceSearchModel>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments)
        {
            return PostAsync(CodexServiceMethod.FindDefLocation, arguments, c => c.FindDefinitionLocationAsync(arguments));
        }

        public Task<IndexQueryResponse<IBoundSourceSearchModel>> GetSourceAsync(GetSourceArguments arguments)
        {
            return PostAsync(CodexServiceMethod.GetSource, arguments, c => c.GetSourceAsync(arguments));
        }

        public Task<IndexQueryHitsResponse<ISearchResult>> SearchAsync(SearchArguments arguments)
        {
            return PostAsync(CodexServiceMethod.Search, arguments, c => c.SearchAsync(arguments));
        }

        private Task<TResult> PostAsync<TArguments, TResult>(
            CodexServiceMethod searchMethod, 
            TArguments arguments, 
            Func<ICodex, Task<TResult>> func)
            where TResult : IndexQueryResponse, new()
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();

            var url = baseUrl + searchMethod.ToString();
            Console.WriteLine(url);

            var argumentsData = JsonConvert.SerializeObject(arguments);

            var config = new JQueryAjaxSettings
            {
                url = url,
                type = "POST",
                data = argumentsData,

                // Set the contentType of the request
                contentType = "application/json; charset=utf-8",

                success = (data, textStatus, successRequest) =>
                {
                    tcs.SetResult(JsonConvert.DeserializeObject<TResult>(successRequest.responseText));
                    return null;
                },

                error = (errorRequest, textStatus, errorThrown) =>
                {
                    tcs.SetResult(new TResult()
                    {
                        Error = $"Error: {errorThrown}"
                    });

                    return null;
                }
            };

            jQuery.ajax(config);

            return tcs.Task;
        }
    }
}
