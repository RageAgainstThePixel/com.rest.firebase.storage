// Licensed under the MIT License. See LICENSE in the project root for license information.

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
        }

        internal FirebaseAuthenticationClient AuthenticationClient { get; }

        public string StorageBucket { get; }

        /// <summary>
        /// Constructs a firebase path to the resource.
        /// </summary>
        /// <param name="name">Name of the resource. This can be a folder, a file name or full path.</param>
        /// <example>
        ///     storage
        ///         .Resource("some")
        ///         .Child("path")
        ///         .Child("to/file.png");
        /// </example>
        /// <returns> <see cref="FirebaseStorageResource"/> for fluid syntax.</returns>
        public FirebaseStorageResource Resource(string name)
        {
            return new FirebaseStorageResource(this, name);
        }
    }
}
