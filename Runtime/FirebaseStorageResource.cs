// Licensed under the MIT License. See LICENSE in the project root for license information.

using Firebase.Authentication;
using Firebase.Authentication.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly List<string> pathParts;
        private readonly FirebaseStorageClient storageClient;

        internal FirebaseStorageResource(FirebaseStorageClient storageClient, string resourcePath, string delimiter = "/")
        {
            this.delimiter = delimiter;
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMilliseconds(-1.0);
            pathParts = new List<string>();
            this.storageClient = storageClient;

            var resourcePathParts = resourcePath.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);

            if (resourcePathParts.Length > 0)
            {
                name = resourcePathParts[0];

                for (int i = 1; i < resourcePathParts.Length; i++)
                {
                    pathParts.Add(resourcePathParts[i]);
                }
            }
            else
            {
                name = string.Empty;
            }
        }

        /// <inheritdoc />
        public override string ToString() => ResourcePath;

        private string ResourcePath => $"{name}{(pathParts.Count > 0 ? delimiter : string.Empty)}{string.Join(delimiter, pathParts)}";

        private string EscapedPath => Uri.EscapeDataString(ResourcePath);

        private string FirebaseBucketUrl => $"{FirebaseStorageEndpoint}{storageClient.StorageBucket}/o";

        private string UploadUrl => $"{FirebaseBucketUrl}?name={EscapedPath}";

        /// <summary>
        /// The full escaped resource url.
        /// </summary>
        public string ResourceUrl => $"{FirebaseBucketUrl}/{EscapedPath}";

        /// <summary>
        /// Upload a given stream to target resource location.
        /// </summary>
        /// <param name="stream"> Stream to upload.</param>
        /// <param name="progress">Optional progress report.</param>
        /// <returns>The download url to the uploaded file.</returns>
        public Task<string> UploadAsync(Stream stream, IProgress<FirebaseStorageProgress> progress = null)
            => UploadAsync(stream, null, progress, CancellationToken.None);

        /// <summary>
        /// Starts uploading given stream to target resource location.
        /// </summary>
        /// <param name="stream"> Stream to upload.</param>
        /// <param name="mimeType">The type of data being uploaded, will be used to set HTTP Content-Type header.</param>
        /// <param name="progress">Optional progress report.</param>
        /// <param name="cancellationToken"> Cancellation token which can be used to cancel the operation.</param>
        /// <returns>The download url to the uploaded file.</returns>
        public Task<string> UploadAsync(Stream stream, string mimeType, IProgress<FirebaseStorageProgress> progress = null, CancellationToken cancellationToken = default)
            => UploadFile(stream, mimeType, progress, cancellationToken);

        private async Task<string> UploadFile(Stream stream, string mimeType, IProgress<FirebaseStorageProgress> progress, CancellationToken cancellationToken = default)
        {
            var responseData = "N/A";

            using var cancelProgressToken = new CancellationTokenSource();

            var _ = Task.Factory.StartNew(ReportProgressLoop, cancelProgressToken.Token);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, UploadUrl)
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
            catch (Exception e)
            {
                switch (e)
                {
                    case FirebaseAuthException:
                    case TaskCanceledException:
                        throw;
                    default:
                        throw new FirebaseStorageException(UploadUrl, responseData, e);
                }
            }
            finally
            {
                cancelProgressToken.Cancel();
            }

            async Task ReportProgressLoop()
            {
                var frame = 0;

                while (true)
                {
                    await Task.Delay(500);

                    frame++;
                    var unit = "b";
                    var speed = (stream.Position * 8) / (frame * 0.5f);

                    if (speed > 1e+2 && speed < 1e+5)
                    {
                        unit = "kb";
                        speed = (float)Math.Round(speed / 1e+3);
                    }
                    else if (speed > 1e+5 && speed < 1e+8)
                    {
                        unit = "mb";
                        speed = (float)Math.Round(speed / 1e+6);
                    }
                    else if (speed > 1e+8 && speed < 1e+11)
                    {
                        unit = "gb";
                        speed = (float)Math.Round(speed / 1e+9);
                    }
                    else if (speed > 1e+11)
                    {
                        unit = "tb";
                        speed = (float)Math.Round(speed / 1e+12);
                    }

                    try
                    {
                        progress?.Report(new FirebaseStorageProgress(stream.Position, stream.Length, speed, unit));
                    }
                    catch (ObjectDisposedException)
                    {
                        // there is no 100% way to prevent ObjectDisposedException, there are bound to be concurrency issues.
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the meta data for given resource.
        /// </summary>
        /// <returns><see cref="StorageObjectMetaData"/> for the associated resource.</returns>
        public async Task<Dictionary<string, object>> GetMetaDataAsync()
        {
            var resultContent = "N/A";

            try
            {
                await SetRequestHeadersAsync().ConfigureAwait(false);
                var result = await httpClient.GetAsync(ResourceUrl).ConfigureAwait(false);
                resultContent = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(resultContent);
                result.EnsureSuccessStatusCode();

                return data;
            }
            catch (Exception e)
            {
                switch (e)
                {
                    case FirebaseAuthException:
                        throw;
                    default:
                        throw new FirebaseStorageException(ResourceUrl, resultContent, e);
                }
            }
        }

        /// <summary>
        /// Gets the url to download given file.
        /// </summary>
        public async Task<string> GetDownloadUrlAsync(Dictionary<string, object> data = null)
        {
            Dictionary<string, object> metaData = data;

            try
            {
                if (data == null)
                {
                    metaData = await GetMetaDataAsync().ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                if (e.Message.Contains("404"))
                {
                    return string.Empty;
                }

                throw;
            }

            object downloadTokens;

            if (!metaData.TryGetValue(nameof(downloadTokens), out downloadTokens))
            {
                throw new ArgumentOutOfRangeException($"Could not extract {nameof(downloadTokens)} property from response!\nResponse: {JsonConvert.SerializeObject(data)}");
            }

            return $"{ResourceUrl}?alt=media&token={downloadTokens}";
        }

        /// <summary>
        /// Deletes a file at target resource location.
        /// </summary>
        public async Task DeleteAsync()
        {
            var resultContent = "N/A";

            try
            {
                await SetRequestHeadersAsync().ConfigureAwait(false);
                var result = await httpClient.DeleteAsync(ResourceUrl).ConfigureAwait(false);
                resultContent = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                if (e is FirebaseAuthException)
                {
                    throw;
                }

                if (e.Message.Contains("404"))
                {
                    return;
                }

                throw new FirebaseStorageException(ResourceUrl, resultContent, e);
            }
        }

        /// <summary>
        /// Retrieves a list of objects matching the criteria, ordered in the list lexicographically by name.
        /// </summary>
        /// <param name="recursive">Should the method list all of the items under this resource recursively?</param>
        /// <param name="prefix">Filter results to include only objects whose names begin with this prefix.</param>
        /// <param name="maxResults">Maximum combined number of entries in items[] and prefixes[] to return in a single page of responses. Because duplicate entries in prefixes[] are omitted, fewer total results may be returned than requested. The service uses this parameter or 1,000 items, whichever is smaller.</param>
        /// <param name="startOffset">Filter results to objects whose names are lexicographically equal to or after startOffset. If endOffset is also set, the objects listed have names between startOffset (inclusive) and endOffset (exclusive).</param>
        /// <param name="endOffset">Filter results to objects whose names are lexicographically before endOffset. If startOffset is also set, the objects listed have names between startOffset (inclusive) and endOffset (exclusive).</param>
        /// <returns></returns>
        public async Task<List<FirebaseStorageResource>> ListItemsAsync(bool recursive = false, string prefix = null, int maxResults = 1000, string startOffset = null, string endOffset = null)
        {
            var responseContent = "N/A";
            var request = $"{FirebaseBucketUrl}?{nameof(delimiter)}={Uri.EscapeUriString(delimiter)}";

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                request += $"&{nameof(prefix)}={Uri.EscapeUriString(prefix)}";
            }
            else
            {
                request += $"&{nameof(prefix)}={Uri.EscapeUriString(ResourcePath)}{Uri.EscapeUriString(delimiter)}";
            }

            if (!string.IsNullOrWhiteSpace(startOffset))
            {
                request += $"&{nameof(startOffset)}={Uri.EscapeUriString(startOffset)}";
            }

            if (!string.IsNullOrWhiteSpace(endOffset))
            {
                request += $"&{nameof(endOffset)}={Uri.EscapeUriString(endOffset)}";
            }

            request += $"&{nameof(maxResults)}={maxResults}&key={storageClient.AuthenticationClient.Configuration.ApiKey}";

            try
            {
                await SetRequestHeadersAsync().ConfigureAwait(false);
                var response = await httpClient.GetAsync(request).ConfigureAwait(false);
                responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                if (e is FirebaseAuthException)
                {
                    throw;
                }

                throw new FirebaseStorageException(request, responseContent, e);
            }

            var result = new List<FirebaseStorageResource>();
            var listResponse = JsonUtility.FromJson<ListResponse>(responseContent);

            for (int i = 0; i < listResponse.Prefixes.Count; i++)
            {
                var resourceFolder = new FirebaseStorageResource(storageClient, listResponse.Prefixes[i], delimiter);
                result.Add(resourceFolder);

                if (recursive)
                {
                    try
                    {
                        result.AddRange(await resourceFolder.ListItemsAsync(true, prefix, maxResults, startOffset, endOffset).ConfigureAwait(false));
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to get {resourceFolder} items!\n{e}");
                    }
                }
            }

            for (var i = 0; i < listResponse.Items.Count; i++)
            {
                result.Add(new FirebaseStorageResource(storageClient, listResponse.Items[i].Name, delimiter));
            }

            return result;
        }

        private async Task SetRequestHeadersAsync()
        {
            var token = await storageClient.AuthenticationClient.User.GetIdTokenAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new FirebaseAuthException($"Failed to get a valid authentication token for {storageClient.AuthenticationClient.User.Uid}", AuthErrorReason.InvalidAccessToken);
            }

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(nameof(Firebase), token);
        }
    }
}
