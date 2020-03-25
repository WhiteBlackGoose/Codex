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
    public class Request
    {
        public static Task<string> GetTextAsync(string url)
        {
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();

            var config = new JQueryAjaxSettings
            {
                url = url,
                type = "GET",

                dataType = "text",

                success = (data, textStatus, successRequest) =>
                {
                    tcs.SetResult(successRequest.responseText);
                    return null;
                },

                error = (errorRequest, textStatus, errorThrown) =>
                {
                    tcs.SetResult($"Error: \n{errorThrown}");
                    return null;
                }
            };

            jQuery.ajax(config);
            return tcs.Task;
        }
    }
}
