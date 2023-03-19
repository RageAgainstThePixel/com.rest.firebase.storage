// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Firebase.Storage
{
    public class FirebaseStorageException : Exception
    {
        public FirebaseStorageException(string url, string responseData, Exception innerException)
            : base(GenerateExceptionMessage(url, responseData), innerException)
        {
            RequestUrl = url;
            ResponseData = responseData;
        }

        /// <summary>
        /// Gets the original request url.
        /// </summary>
        public string RequestUrl { get; }

        /// <summary>
        /// Gets the response data returned by the firebase service.
        /// </summary>
        public string ResponseData { get; }

        private static string GenerateExceptionMessage(string requestUrl, string responseData)
            => $"Exception occurred while processing the request.\nUrl: {requestUrl}\nResponse: {responseData}";
    }
}
