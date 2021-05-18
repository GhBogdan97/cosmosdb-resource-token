using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Azure.Documents.Client;
using System.Security;
using System.Net;
using Microsoft.Azure.Documents;
using System.Linq;

namespace ResourceTokenBroker
{
    public static class RequestAccess
    {
        private static CosmosClient cosmosClient;
        private static DocumentClient Client;

        // The name of the database and container we will create
        private static string databaseId = "Contoso";
        private static string containerId = "HR Documents"; //collection id when using mongo


        private static string BrokerConnectionString = $@"AccountKey=etqJ9x5PqQyzePwoQh6sf37j7yQcgOujGVbXpwoLFyHGzFuV7KtrSre0WV3JbUPOMmQsq7wjCXYiLwg3hdBr5w==;
                                                            AccountEndpoint=https://cosmos-sql-weu-resource-token.documents.azure.com:443/";


        [FunctionName("RequestAccess")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, TraceWriter log)
        {
            log.Info("Cosmos Client Implementation");

            try
            {
                await CosmosClientImplementation(log);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }

            log.Info("Document Client Implementation");

            try
            {
                await DocumentClientImplementation(log);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }

            return new OkObjectResult("");

        }

        private static async Task<int> QueryItemsAsync(Container container, TraceWriter log)
        {
            var sqlQueryText = "SELECT * FROM c";

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            FeedIterator<TestClass> queryResultSetIterator = container.GetItemQueryIterator<TestClass>(queryDefinition);

            log.Info("query executed");

            List<TestClass> families = new List<TestClass>();
            int x = 0;


            while (queryResultSetIterator.HasMoreResults)
            {
                log.Info("in while");
                Microsoft.Azure.Cosmos.FeedResponse<TestClass> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (TestClass family in currentResultSet)
                {
                    x++;
                }
            }
            log.Info($"objects found: ${x}");

            return x;

        }

        private static async Task<string> CosmosClientImplementation(TraceWriter log)
        {
            cosmosClient = new CosmosClient(BrokerConnectionString);

            Container container = cosmosClient.GetContainer(databaseId, containerId);
            var token = "";
            var user = cosmosClient.GetDatabase(databaseId).GetUser("ContosoAccessUser");
            log.Info($"user found {user.Id}");
            try
            {
                var permissions = await user.ReadAsync();
                var p = user.GetPermission("ContosoAccessUser");
                var res = await p.ReadAsync();
                token = res.Resource.Token;
                log.Info($"permissions found: {token}");
            }
            catch (Exception)
            {
                var response = await user.CreatePermissionAsync(
                   new PermissionProperties(
                       id: "ContosoAccessUser",
                       permissionMode: Microsoft.Azure.Cosmos.PermissionMode.All,
                       container: container));

                token = response.Resource.Token;
                log.Info($"permissions created: {token}");
            }


            cosmosClient = new CosmosClient(accountEndpoint: cosmosClient.Endpoint.AbsoluteUri, authKeyOrResourceToken: token);
            log.Info($"new client created");
            Container newContainer = cosmosClient.GetContainer(databaseId, containerId);
            log.Info($"new container created");
            var queryOutcome = (await QueryItemsAsync(newContainer, log)).ToString();
            log.Info(queryOutcome);

            return token;
        }

        private static async Task<string> DocumentClientImplementation(TraceWriter log)
        {
            Client = new DocumentClient(new Uri("https://cosmos-sql-weu-resource-token.documents.azure.com:443/"), new NetworkCredential("", "etqJ9x5PqQyzePwoQh6sf37j7yQcgOujGVbXpwoLFyHGzFuV7KtrSre0WV3JbUPOMmQsq7wjCXYiLwg3hdBr5w==").SecurePassword);
            Microsoft.Azure.Documents.User u = await Client.ReadUserAsync(UriFactory.CreateUserUri(databaseId, "ContosoAccessUser"));

            Microsoft.Azure.Documents.Permission p;
            var docToken = "";

            try
            {
                Microsoft.Azure.Documents.Client.FeedResponse<Microsoft.Azure.Documents.Permission> permissionFeed = await Client.ReadPermissionFeedAsync(UriFactory.CreateUserUri(databaseId, "ContosoAccessUser"));
                var permissions = permissionFeed.ToList();

                var permission = permissions.Where(p => p.ResourceLink == $"/dbs/{databaseId}/colls/{containerId}").FirstOrDefault();
                docToken = permission.Token;
            }
            catch (Exception ex)
            {
                var p2 = new Microsoft.Azure.Documents.Permission
                {
                    PermissionMode = Microsoft.Azure.Documents.PermissionMode.All,
                    ResourceLink = $"/dbs/{databaseId}/colls/{containerId}",
                    //ResourcePartitionKey = new Microsoft.Azure.Documents.PartitionKey("/location"),
                    Id = new Guid().ToString() //needs to be unique for a given user
                };
                Microsoft.Azure.Documents.Permission permission = await Client.CreatePermissionAsync(u.SelfLink, p2);
                docToken = permission.Token;
            }

            log.Info($"token {docToken}");

            var Client2 = new DocumentClient(new Uri("https://cosmos-sql-weu-resource-token.documents.azure.com:443/"), new NetworkCredential("", docToken).SecurePassword);
            log.Info($"new client created");

            log.Info($"read database");

            var db = await Client2.ReadDatabaseAsync(Microsoft.Azure.Documents.Client.UriFactory.CreateDatabaseUri(databaseId));
            var response = await Client2.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseId, containerId, "7a17c459-31f4-4af8-aac2-ffef8c3e3c4a"));
            var x = (TestClass)(dynamic)response.Resource;

            return docToken;
        }

        class TestClass
        {
            public string Name { get; set; }

            [JsonPropertyName("id")]
            public string Id { get; set; }

            public string Location { get; set; }
        }
    }
}