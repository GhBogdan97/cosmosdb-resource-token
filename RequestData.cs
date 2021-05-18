
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Net.Http;

namespace ResourceAPI
{
    public static class RequestData
    {
        private static string databaseId = "Contoso";
        private static string containerId = "HR Documents"; //collection id when using mongo

        [FunctionName("RequestData")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            //CosmosClient client = new CosmosClient(accountEndpoint: "https://cosmos-sql-weu-resource-token.documents.azure.com", authKeyOrResourceToken: token);

            using (IClientWrapper client = new ClientResourceTokenWrapper())
            {
                Container newContainer = client.GetContainer(databaseId, containerId);
                var queryOutcome = (await QueryItemsAsync(newContainer)).ToString();
                log.Info(queryOutcome);

                return new OkObjectResult(queryOutcome);
            }
            

        }

        private static async Task<long> QueryItemsAsync(Container container)
        {
            var sqlQueryText = "SELECT * FROM c";


            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            FeedIterator<TestClass> queryResultSetIterator = container.GetItemQueryIterator<TestClass>(queryDefinition);

            List<TestClass> families = new List<TestClass>();
            long x = 0;

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<TestClass> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (TestClass family in currentResultSet)
                {
                    x++;
                }
            }
            return x;
        }

        class TestClass
        {
            public string Id { get; set; }

            public string Location { get; set; }
        }

        interface IClientWrapper : IDisposable
        {
            Container GetContainer(string databaseId, string containerId);
        }

        class ClientResourceTokenWrapper: IClientWrapper
        {
            private string accountEndpoint = "https://cosmos-sql-weu-resource-token.documents.azure.com";
            private string tokenBrokerUrl = "https://fa-noe-resource-token.azurewebsites.net/api/RequestAccess?code=pccN4rJq7vXHIKrB2wkkQTACtEHp5JBNsn5iBYthah11FSBXitCf5g==";
            CosmosClient client;
            public ClientResourceTokenWrapper()
            {
                var task = EnsureAccess();
                task.Wait();
                var token = task.Result;
                client = new CosmosClient(accountEndpoint: accountEndpoint, authKeyOrResourceToken: token);
            }

            private async Task<string> EnsureAccess()
            {
                var httpClient = new HttpClient();
                var result = await httpClient.GetAsync(tokenBrokerUrl);
                return await result.Content.ReadAsStringAsync();
            }

            public Container GetContainer(string databaseId, string containerId)
            {
                return client.GetContainer(databaseId, containerId); ;
            }

            public void Dispose()
            {
                
            }
        }
    }
}
