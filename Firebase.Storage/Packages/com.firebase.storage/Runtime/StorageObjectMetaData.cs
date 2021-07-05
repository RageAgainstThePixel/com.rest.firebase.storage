// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Firebase.Storage
{
    /// <summary>
    /// Full list of meta data available here: https://firebase.google.com/docs/storage/web/file-metadata
    /// </summary>
    [Serializable]
    public class StorageObjectMetaData
    {
        [SerializeField]
        private string name;

        public string Name => name;

        [SerializeField]
        private string bucket;

        public string Bucket => bucket;

        [SerializeField]
        private string kind;

        public string Kind => kind;

        [SerializeField]
        private string id;

        public string Id => id;

        [SerializeField]
        private string selfLink;

        public string SelfLink => selfLink;

        [SerializeField]
        private string mediaLink;

        private string MediaLink => mediaLink;

        [SerializeField]
        private string contentType;

        private string ContentType => contentType;

        [SerializeField]
        private string size;

        private int Size => int.Parse(size);

        [SerializeField]
        private string timeCreated;

        public DateTime TimeCreate => DateTime.Parse(timeCreated);

        [SerializeField]
        private string updated;

        public DateTime Updated => DateTime.Parse(updated);

        [SerializeField]
        private string md5Hash;

        public string Md5Hash => md5Hash;

        [SerializeField]
        private string contentEncoding;

        public string ContentEncoding => contentEncoding;

        [SerializeField]
        private string contentDisposition;

        public string ContentDisposition => contentDisposition;
    }
}
