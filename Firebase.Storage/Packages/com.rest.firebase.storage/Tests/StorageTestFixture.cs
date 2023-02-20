// Licensed under the MIT License. See LICENSE in the project root for license information.

using Firebase.Authentication;
using Firebase.Authentication.Tests;
using NUnit.Framework;
using System;
using System.IO;
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

            UnityTestUtils.RunAsyncTestsAsSync(async () =>
            {
                var firebaseClient = new FirebaseAuthenticationClient();
                var fetchResult = await firebaseClient.FetchSignInMethodsForEmailAsync(email);

                if (fetchResult.UserExists)
                {
                    await firebaseClient.SignInWithEmailAndPasswordAsync(email, password);
                }
                else
                {
                    await firebaseClient.CreateUserWithEmailAndPasswordAsync(email, password);
                }

                var firebaseStorageClient = new FirebaseStorageClient(firebaseClient);

                Assert.IsNotNull(firebaseStorageClient);

                var restResource = firebaseStorageClient.Resource("root/test.json");

                var json = "{\"value\":\"42\"}";

                string downloadUrl;

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    downloadUrl = await restResource.UploadAsync(stream);
                }

                var knownUrl = await restResource.GetDownloadUrlAsync();

                Assert.IsTrue(downloadUrl == knownUrl);

                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(knownUrl);
                var responseData = await response.Content.ReadAsStringAsync();
                Assert.IsTrue(responseData == json);

                var resources = await firebaseStorageClient.ListItemsAsync();

                Assert.IsNotEmpty(resources);

                await restResource.DeleteAsync();
                await firebaseClient.User.DeleteAsync();
            });
        }
    }
}
