// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Firebase.Storage
{
    [Serializable]
    internal class StorageObject
    {
        [SerializeField]
        private string name;

        public string Name => name;

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
        private DateTime timeCreated;

        public DateTime TimeCreate => timeCreated;

        [SerializeField]
        private DateTime updated;

        public DateTime Updated => updated;
    }
}
