using Bridge.Html5;
using Codex.ObjectModel;
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

        public Task<IndexQueryResponse<ReferencesResult>> FindAllReferencesAsync(FindAllReferencesArguments arguments)
        {
            return PostAsync<IndexQueryResponse<SerializedReferencesResult>, IndexQueryResponse<ReferencesResult>>(CodexServiceMethod.FindAllRefs, arguments);
        }

        public Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments)
        {
            return PostAsync<IndexQueryHitsResponse<DefinitionSearchModel>, IndexQueryHitsResponse<IDefinitionSearchModel>>(CodexServiceMethod.FindDef, arguments);
        }

        public Task<IndexQueryResponse<ReferencesResult>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments)
        {
            return PostAsync<IndexQueryResponse<SerializedReferencesResult>, IndexQueryResponse<ReferencesResult>>(CodexServiceMethod.FindDefLocation, arguments);
        }

        public Task<IndexQueryResponse<IBoundSourceFile>> GetSourceAsync(GetSourceArguments arguments)
        {
            return PostAsync<IndexQueryResponse<BoundSourceFile>, IndexQueryResponse<IBoundSourceFile>>(CodexServiceMethod.GetSource, arguments);
        }

        public Task<IndexQueryHitsResponse<ISearchResult>> SearchAsync(SearchArguments arguments)
        {
            if (arguments.SearchString == "tdt")
            {
                return GetTestAsync<IndexQueryHitsResponse<SearchResult>, IndexQueryHitsResponse<ISearchResult>>(CodexServiceMethod.Search, arguments);
            }

            return PostAsync<IndexQueryHitsResponse<SearchResult>, IndexQueryHitsResponse<ISearchResult>>(CodexServiceMethod.Search, arguments);
        }

        private Task<TResult> GetTestAsync<TSerializedResult, TResult>(
            CodexServiceMethod searchMethod,
            object arguments)
            where TResult : IndexQueryResponse, new()
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();

            var url = baseUrl + searchMethod.ToString();
            Console.WriteLine(url);

            var config = new JQueryAjaxSettings
            {
                url = "testsearchdata.json",
                type = "GET",

                dataType = "json",

                success = (data, textStatus, successRequest) =>
                {
                    tcs.SetResult(JsonConvert.DeserializeObject<TSerializedResult>(successRequest.responseText).As<TResult>());
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

        private Task<TResult> PostAsync<TSerializedResult, TResult>(
            CodexServiceMethod searchMethod,
            object arguments)
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
                    tcs.SetResult(JsonConvert.DeserializeObject<TSerializedResult>(successRequest.responseText).As<TResult>());
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

    internal class SerializedReferencesResult : IndexQueryHits<ReferenceSearchResult>
    {
        public string SymbolDisplayName { get; set; }
    }
}
