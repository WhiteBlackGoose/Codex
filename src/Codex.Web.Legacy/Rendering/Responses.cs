using System;
using System.Web;
using System.Web.Mvc;
using Codex.Sdk.Search;

namespace WebUI
{
    public static class Responses
    {
        public static T ThrowOnError<T>(this T response)
            where T : IndexQueryResponse
        {
            if (!string.IsNullOrEmpty(response.Error))
            {
                throw new Exception(response.Error);
            }

            return response;
        }

        public static ContentResult Exception(Exception ex)
        {
            return Message($"<pre>{ex.ToString()}</pre>");
        }

        public static ContentResult Message(string text)
        {
            return new ContentResult { Content = $"<div class=\"note\">{text}</div>" };
        }

        public static void PrepareResponse(HttpResponseBase response)
        {
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Pragma", "no-cache");
            response.Headers.Add("Expires", "-1");
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
        }
    }
}