// Licensed under the MIT License. See LICENSE in the project root for license information.

using Firebase.Authentication;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using UnityEngine;

namespace Firebase.Storage.Tests
{
    internal class StorageTestFixture
    {
        [Serializable]
        private struct TestJson
        {
            public TestJson(Guid guid)
            {
                id = guid.ToString();
            }

            [SerializeField]
            private string id;

            public Guid Id => Guid.Parse(id);
        }

        [Test]
        public void Test_1_CreateClient()
        {
            var firebaseClient = new FirebaseAuthenticationClient();
            var firebaseStorageClient = new FirebaseStorageClient(firebaseClient);

            Assert.IsNotNull(firebaseStorageClient);
        }

        [Test]
        public void Test_2_UploadDownloadFile()
        {
            const string email = "test@email.com";
            const string password = "tempP@ssw0rd";

            var firebaseClient = new FirebaseAuthenticationClient();
            var fetchResult = firebaseClient.FetchSignInMethodsForEmailAsync(email).Result;

            if (fetchResult.SignInProviders.Contains(FirebaseProviderType.EmailAndPassword))
            {
                firebaseClient.SignInWithEmailAndPasswordAsync(email, password).Wait();
            }
            else
            {
                firebaseClient.CreateUserWithEmailAndPasswordAsync(email, password).Wait();
            }

            var firebaseStorageClient = new FirebaseStorageClient(firebaseClient);

            Assert.IsNotNull(firebaseStorageClient);

            var resource = firebaseStorageClient.Resource("root/test.json");

            var json = "{\"value\":\"42\"}";

            string downloadUrl;

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                downloadUrl = resource.UploadAsync(stream, new Progress<FirebaseStorageProgress>(progress => Debug.Log(progress.Percentage))).Result;
            }

            var knownUrl = resource.GetDownloadUrlAsync().Result;

            Assert.IsTrue(downloadUrl == knownUrl);

            var httpClient = new HttpClient();
            var response = httpClient.GetAsync(knownUrl).Result;
            var responseData = response.Content.ReadAsStringAsync().Result;
            Assert.IsTrue(responseData == json);

            resource.DeleteAsync().Wait();
            firebaseClient.User.DeleteAsync().Wait();
        }
    }
}
