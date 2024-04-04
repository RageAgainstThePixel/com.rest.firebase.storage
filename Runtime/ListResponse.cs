// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Firebase.Rest.Storage
{
    [Serializable]
    internal class ListResponse
    {
        [SerializeField]
        private string kind;

        public string Kind => kind;

        [SerializeField]
        private List<string> prefixes;

        public IReadOnlyList<string> Prefixes => prefixes;

        [SerializeField]
        private List<StorageObjectMetaData> items;

        public IReadOnlyList<StorageObjectMetaData> Items => items;
    }
}
