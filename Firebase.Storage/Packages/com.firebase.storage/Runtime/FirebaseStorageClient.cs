// Licensed under the MIT License. See LICENSE in the project root for license information.

using Firebase.Authentication;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web;

namespace Firebase.Storage
{
    /// <summary>
    /// A Firebase client which encapsulates authenticated communication with Firebase storage services.
    /// </summary>
    public class FirebaseStorageClient
    {
        /// <summary>
        /// Creates a new <see cref="FirebaseStorageClient"/>.
        /// </summary>
        /// <param name="authenticationClient"><see cref="FirebaseAuthenticationClient"/>.</param>
        /// <param name="storageBucket">Optional, override storage bucket to use.</param>
        public FirebaseStorageClient(FirebaseAuthenticationClient authenticationClient, string storageBucket = null)
        {
            AuthenticationClient = authenticationClient;
            StorageBucket = storageBucket ?? $"{authenticationClient.Configuration.ProjectId}.appspot.com";
            topLevelResource = new FirebaseStorageResource(this, string.Empty);
            resourceCache = new Dictionary<string, FirebaseStorageResource>();
        }

        private readonly FirebaseStorageResource topLevelResource;
        private readonly Dictionary<string, FirebaseStorageResource> resourceCache;

        internal FirebaseAuthenticationClient AuthenticationClient { get; }

        public string StorageBucket { get; }

        /// <summary>
        /// Constructs a firebase path to the resource.
        /// </summary>
        /// <param name="name">Name of the resource. This can be a folder, a file name or full path.</param>
        /// <param name="delimiter">Directory-like mode, with "/" being a common value for the delimiter.</param>
        /// <example>
        /// storage.Resource("some/path/to/file.png");
        /// </example>
        /// <returns>A <see cref="FirebaseStorageResource"/>.</returns>
        public FirebaseStorageResource Resource(string name, string delimiter = "/")
        {
            if (!resourceCache.TryGetValue(name, out var resource))
            {
                resource = new FirebaseStorageResource(this, name, delimiter);
                resourceCache.Add(name, resource);
            }

            return resource;
        }

        /// <summary>
        /// Lists all the top level resources in the bucket.
        /// </summary>
        /// <returns>The list of <see cref="FirebaseStorageResource"/> items in the top level of the bucket.</returns>
        public async Task<List<FirebaseStorageResource>> ListItemsAsync() => await topLevelResource.ListItemsAsync();

        /// <summary>
        /// Upload a provided file path to a remote resource location.
        /// </summary>
        /// <param name="localPath">The local file path.</param>
        /// <param name="remotePath">The remote path to upload it to.</param>
        /// <param name="progress">Optional, <see cref="IProgress{T}"/>.</param>
        /// <returns>The download url to the uploaded file.</returns>
        public async Task<string> UploadFileAsync(string localPath, string remotePath, IProgress<FirebaseStorageProgress> progress = null)
            => await UploadFileAsync(localPath, remotePath, null, progress);

        /// <summary>
        /// Upload a provided file path to a remote resource location.
        /// </summary>
        /// <param name="localPath">The local file path.</param>
        /// <param name="remotePath">The remote path to upload it to.</param>
        /// <param name="mimeMapping"></param>
        /// <param name="progress">Optional, <see cref="IProgress{T}"/>.</param>
        /// <returns>The download url to the uploaded file.</returns>
        public async Task<string> UploadFileAsync(string localPath, string remotePath, string mimeMapping, IProgress<FirebaseStorageProgress> progress = null)
        {
            if (string.IsNullOrWhiteSpace(mimeMapping))
            {
                try
                {
                    mimeMapping = MimeMapping.GetMimeMapping(Path.GetFileName(localPath));
                }
                catch (NotSupportedException)
                {
                    // fallback to octet-stream
                    mimeMapping = "application/octet-stream";
                }
            }

            using (var fileStream = File.OpenRead(localPath))
            {
                return await Resource($"{remotePath}/{Path.GetFileName(localPath)}").UploadAsync(fileStream, mimeMapping, progress);
            }
        }
    }
}
