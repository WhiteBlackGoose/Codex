using System.Threading.Tasks;
using Nest;

namespace Codex.Storage.ElasticProviders
{
    internal static class ResponseValidator
    {
        public static async Task<T> ThrowOnFailure<T>(this Task<T> result, bool allowInvalid = false) where T : IResponse
        {
            return (await result).ThrowOnFailure(allowInvalid);
        }

        public static T ThrowOnFailure<T>(this T result, bool allowInvalid = false) where T : IResponse
        {
            if (result == null)
            {
                return default(T);
            }

            bool failed = !result.ApiCall.Success || (!allowInvalid && !result.IsValid);
            if (result is IBulkResponse bulkResponse)
            {
                failed |= bulkResponse.Errors;
            }

            if (failed)
            {
                throw new ElasticProviderCommunicationException(result, result.DebugInformation);
            }

            return result;
        }
    }
}