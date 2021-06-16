// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Firebase.Storage
{
    public class FirebaseStorageResource
    {
        private const string FirebaseStorageEndpoint = "https://firebasestorage.googleapis.com/v0/b/";

        private readonly List<string> children;
        private readonly HttpClient httpClient;
        private readonly FirebaseStorageClient storageClient;

        internal FirebaseStorageResource(FirebaseStorageClient storageClient, string name)
        {
            this.storageClient = storageClient;
            httpClient = new HttpClient();
            children = new List<string> { name };
        }

        private string EscapedPath
            => Uri.EscapeDataString(string.Join("/", children));

        private string TargetUrl
            => $"{FirebaseStorageEndpoint}{storageClient.StorageBucket}/o?name={EscapedPath}";

        private string DownloadUrl
            => $"{FirebaseStorageEndpoint}{storageClient.StorageBucket}/o/{EscapedPath}";

        private string FullDownloadUrl
            => $"{DownloadUrl}?alt=media&token=";

        /// <summary>
        /// Gets the meta data for given file.
        /// </summary>
        /// <returns></returns>
        public async Task<FirebaseMetaData> GetMetaDataAsync()
            => await PerformFetch<FirebaseMetaData>();

        /// <summary>
        /// Upload a given stream to target location.
        /// </summary>
        /// <param name="stream"> Stream to upload.</param>
        /// <param name="progress">Optional progress report.</param>
        /// <returns>The download url to the uploaded file.</returns>
        public Task<string> UploadAsync(Stream stream, IProgress<FirebaseStorageProgress> progress = null)
            => UploadAsync(stream, null, progress, CancellationToken.None);

        /// <summary>
        /// Starts uploading given stream to target location.
        /// </summary>
        /// <param name="stream"> Stream to upload.</param>
        /// <param name="mimeType">Optional type of data being uploaded, will be used to set HTTP Content-Type header.</param>
        /// <param name="progress">Optional progress report.</param>
        /// <param name="cancellationToken"> Cancellation token which can be used to cancel the operation.</param>
        /// <returns>The download url to the uploaded file.</returns>
        public Task<string> UploadAsync(Stream stream, string mimeType = null, IProgress<FirebaseStorageProgress> progress = null, CancellationToken cancellationToken = default)
            => UploadFile(stream, mimeType, progress, cancellationToken);

        private async Task<string> UploadFile(Stream stream, string mimeType, IProgress<FirebaseStorageProgress> progress, CancellationToken cancellationToken = default)
        {
            var responseData = "N/A";

            using (var cancelProgressToken = new CancellationTokenSource())
            {
                var _ = Task.Factory.StartNew(ReportProgressLoop, cancelProgressToken.Token);

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, TargetUrl)
                    {
                        Content = new StreamContent(stream)
                    };

                    if (!string.IsNullOrEmpty(mimeType))
                    {
                        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                    }

                    await SetRequestHeadersAsync().ConfigureAwait(false);
                    var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    responseData = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    response.EnsureSuccessStatusCode();
                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseData);

                    return await GetDownloadUrlAsync(data).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw new FirebaseStorageException(TargetUrl, responseData, e);
                }
                finally
                {
                    cancelProgressToken.Cancel();
                }
            }

            async Task ReportProgressLoop()
            {
                while (true)
                {
                    await Task.Delay(500);

                    try
                    {
                        progress?.Report(new FirebaseStorageProgress(stream.Position, stream.Length));
                    }
                    catch (ObjectDisposedException)
                    {
                        // there is no 100 % way to prevent ObjectDisposedException, there are bound to be concurrency issues.
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the url to download given file.
        /// </summary>
        public async Task<string> GetDownloadUrlAsync(Dictionary<string, object> data = null)
        {
            data = data ?? await PerformFetch<Dictionary<string, object>>().ConfigureAwait(false);

            object downloadTokens;

            if (!data.TryGetValue(nameof(downloadTokens), out downloadTokens))
            {
                throw new ArgumentOutOfRangeException($"Could not extract {nameof(downloadTokens)} property from response!\nResponse: {JsonConvert.SerializeObject(data)}");
            }

            return $"{FullDownloadUrl}{downloadTokens}";
        }

        /// <summary>
        /// Deletes a file at target location.
        /// </summary>
        public async Task DeleteAsync()
        {
            var resultContent = "N/A";

            try
            {
                await SetRequestHeadersAsync().ConfigureAwait(false);
                var result = await httpClient.DeleteAsync(DownloadUrl).ConfigureAwait(false);
                resultContent = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                throw new FirebaseStorageException(DownloadUrl, resultContent, e);
            }
        }

        /// <summary>
        /// Constructs firebase path to the file.
        /// </summary>
        /// <param name="name"> Name of the entity. This can be folder or a file name or full path.</param>
        /// <example>
        ///     storage
        ///         .Location("some")
        ///         .Child("path")
        ///         .Child("to/file.png");
        /// </example>
        /// <returns> <see cref="FirebaseStorageResource"/> for fluid syntax.</returns>
        public FirebaseStorageResource Child(string name)
        {
            children.Add(name);
            return this;
        }

        private async Task<T> PerformFetch<T>()
        {
            var resultContent = "N/A";

            try
            {
                await SetRequestHeadersAsync().ConfigureAwait(false);
                var result = await httpClient.GetAsync(DownloadUrl).ConfigureAwait(false);
                resultContent = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var data = JsonConvert.DeserializeObject<T>(resultContent);
                result.EnsureSuccessStatusCode();

                return data;
            }
            catch (Exception e)
            {
                throw new FirebaseStorageException(DownloadUrl, resultContent, e);
            }
        }

        private async Task SetRequestHeadersAsync()
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Firebase", await storageClient.AuthenticationClient.User.GetIdTokenAsync().ConfigureAwait(false));
        }
    }
}
