// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Authentication;

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
        }

        private readonly FirebaseStorageResource topLevelResource;

        internal FirebaseAuthenticationClient AuthenticationClient { get; }

        public string StorageBucket { get; }

        /// <summary>
        /// Constructs a firebase path to the resource.
        /// </summary>
        /// <param name="name">Name of the resource. This can be a folder, a file name or full path.</param>
        /// <param name="delimiter">Directory-like mode, with "/" being a common value for the delimiter.</param>
        /// <example>
        /// // Fluid syntax.
        /// storage.Resource("some/path/to/file.png");
        /// // Object composition syntax.
        /// storage.Resource("some")
        ///        .Child("path")
        ///        .Child("to/file.png");
        /// </example>
        /// <returns>A <see cref="FirebaseStorageResource"/> for fluid syntax.</returns>
        public FirebaseStorageResource Resource(string name, string delimiter = "/") => new FirebaseStorageResource(this, name, delimiter);

        /// <summary>
        /// Lists all the top level resources in the bucket.
        /// </summary>
        /// <returns>The list of <see cref="FirebaseStorageResource"/> items in the top level of the bucket.</returns>
        public async Task<List<FirebaseStorageResource>> ListItemsAsync() => await topLevelResource.ListItems();
    }
}
