// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Firebase.Storage
{
    public class FirebaseStorageResource
    {
        private const string FirebaseStorageEndpoint = "https://firebasestorage.googleapis.com/v0/b/";

        private readonly string name;
        private readonly string delimiter;
        private readonly HttpClient httpClient;
        private readonly FirebaseStorageClient storageClient;
        private readonly Dictionary<string, FirebaseStorageResource> children;

        internal FirebaseStorageResource(FirebaseStorageClient storageClient, string resourcePath, string delimiter = "/")
        {
            this.storageClient = storageClient;
            httpClient = new HttpClient();
            this.delimiter = delimiter;
            children = new Dictionary<string, FirebaseStorageResource>();

            var resourcePathParts = resourcePath.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);

            name = resourcePathParts.Length > 0 ? resourcePathParts[0] : string.Empty;
        }

        /// <inheritdoc />
        public override string ToString() => ResourcePath;

        private string ResourcePath => $"{name}{delimiter}{string.Join(delimiter, children.Keys)}";

        private string EscapedPath
            => Uri.EscapeDataString(ResourcePath);

        private string BaseObjectUrl => $"{FirebaseStorageEndpoint}{storageClient.StorageBucket}/o";

        private string TargetUrl
            => $"{BaseObjectUrl}?name={EscapedPath}";

        private string DownloadUrl
            => $"{BaseObjectUrl}/{EscapedPath}";

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
        /// Constructs firebase path to the child resource.
        /// </summary>
        /// <param name="childName"> Name of the entity. This can be folder, a file name or full path.</param>
        /// <example>
        /// // Fluid syntax.
        /// storage.Resource("some/path/to/file.png");
        /// // Object composition syntax.
        /// storage.Resource("some")
        ///        .Child("path")
        ///        .Child("to/file.png");
        /// </example>
        /// <returns>A <see cref="FirebaseStorageResource"/> for fluid syntax.</returns>
        public FirebaseStorageResource Child(string childName)
        {
            var childResourcePath = $"{name}{delimiter}{childName}";

            if (!children.TryGetValue(childResourcePath, out var childResource))
            {
                childResource = new FirebaseStorageResource(storageClient, childResourcePath, delimiter);
                children.Add(childResourcePath, childResource);
            }

            return childResource;
        }

        /// <summary>
        /// Retrieves a list of objects matching the criteria, ordered in the list lexicographically by name.
        /// </summary>
        /// <param name="prefix">Filter results to include only objects whose names begin with this prefix.</param>
        /// <param name="maxResults">Maximum combined number of entries in items[] and prefixes[] to return in a single page of responses. Because duplicate entries in prefixes[] are omitted, fewer total results may be returned than requested. The service uses this parameter or 1,000 items, whichever is smaller.</param>
        /// <param name="startOffset">Filter results to objects whose names are lexicographically equal to or after startOffset. If endOffset is also set, the objects listed have names between startOffset (inclusive) and endOffset (exclusive).</param>
        /// <param name="endOffset">Filter results to objects whose names are lexicographically before endOffset. If startOffset is also set, the objects listed have names between startOffset (inclusive) and endOffset (exclusive).</param>
        /// <returns></returns>
        public async Task<List<FirebaseStorageResource>> ListItems(string prefix = null, int maxResults = 1000, string startOffset = null, string endOffset = null)
        {
            var responseContent = "N/A";
            var request = $"{BaseObjectUrl}?{Uri.EscapeUriString(delimiter)}&{nameof(maxResults)}={maxResults}";

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                request += $"{nameof(prefix)}={prefix}";
            }

            if (!string.IsNullOrWhiteSpace(startOffset))
            {
                request += $"{nameof(startOffset)}={startOffset}";
            }

            if (!string.IsNullOrWhiteSpace(endOffset))
            {
                request += $"{nameof(endOffset)}={endOffset}";
            }

            request += $"&key={storageClient.AuthenticationClient.Configuration.ApiKey}";

            try
            {
                await SetRequestHeadersAsync().ConfigureAwait(false);
                var response = await httpClient.GetAsync(request).ConfigureAwait(false);
                responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                throw new FirebaseStorageException(request, responseContent, e);
            }

            var result = JsonUtility.FromJson<ListResponse>(responseContent);

            foreach (var resultPrefix in result.Prefixes)
            {
                if (!children.TryGetValue(resultPrefix, out _))
                {
                    children.Add(resultPrefix, new FirebaseStorageResource(storageClient, resultPrefix, delimiter));
                }
            }

            foreach (var storageObject in result.Items)
            {
                if (!children.TryGetValue(storageObject.Name, out _))
                {
                    children.Add(storageObject.Name, new FirebaseStorageResource(storageClient, storageObject.Name, delimiter));
                }
            }

            return children.Values.ToList();
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
