
using System.IO;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.IdentityModel.Tokens.Jwt;

namespace ResourceTokenBroker
{
    public static class RequestAccess
    {
        private static CosmosClient cosmosClient;

        // The database we will create
        //private static Database database;

        // The container we will create.
        //private static Container container;

        // The name of the database and container we will create
        private static string databaseId = "Contoso";
        private static string containerId = "HR Documents"; //collection id when using mongo

        //private static string resourceToken = ""; 

        //private static string ConnectionString = $"mongodb://cosmos-mongo-weu-resource-token:{resourceToken}@cosmos-mongo-weu-resource-token.mongo.cosmos.azure.com:10255/?ssl=true&replicaSet=globaldb&retrywrites=false&maxIdleTimeMS=120000&appName=@cosmos-mongo-weu-resource-token@";
        private static string BrokerConnectionString = $@"AccountKey=etqJ9x5PqQyzePwoQh6sf37j7yQcgOujGVbXpwoLFyHGzFuV7KtrSre0WV3JbUPOMmQsq7wjCXYiLwg3hdBr5w==;
                                                            AccountEndpoint=https://cosmos-sql-weu-resource-token.documents.azure.com:443/";

        //static readonly string utc_date = DateTime.UtcNow.ToString("r");

        [FunctionName("RequestAccess")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {

            cosmosClient = new CosmosClient(BrokerConnectionString);
            Container container = cosmosClient.GetContainer(databaseId, containerId);
            var token = "";

            var user = cosmosClient.GetDatabase(databaseId).GetUser("ContosoAccessUser");
            log.Info($"user found {user.Id}"); 
            try
            {
                var p = user.GetPermission("ContosoAccessUser");
                token = (await p.ReadAsync()).Resource.Token;
                log.Info($"permissions found: {token}");
            }
            catch (Exception)
            {
                var response = await user.CreatePermissionAsync(
                   new PermissionProperties(
                       id: "ContosoAccessUser",
                       permissionMode: PermissionMode.All,
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

            return new OkObjectResult(token);

            

            //bool idBased = true;
            //var masterKey = "AKUUoYT0599sr5odhZoNuZJsA26yI8Knk2Lqm0uTAGzLM5CLhUE5WxCWX7j0yXqD0LNmb5HMF1dnzwJvqpFC3Q==";
            //var verb = "GET";
            //var resourceType = "docs";
            //var resourceLink = string.Format("dbs/{0}/colls/{1}/docs", databaseId, containerId);
            //var resourceId = (idBased) ? string.Format("dbs/{0}/colls/{1}", databaseId, containerId) : containerId.ToLowerInvariant();
            //var authHeader = GenerateMasterKeyAuthorizationSignature(verb, resourceId, resourceType, masterKey, "PrimaryMasterKey", "1.0");

            //log.Info(authHeader);

            //var client = new System.Net.Http.HttpClient();
            //client.DefaultRequestHeaders.Add("x-ms-date", utc_date);
            //client.DefaultRequestHeaders.Add("x-ms-version", "2018-12-31");
            //client.DefaultRequestHeaders.Add("authorization", authHeader);
            //var response = client.GetStringAsync(new Uri("https://cosmos-mongo-weu-resource-token.documents.azure.com/"+ resourceLink)).Result;

            //user.CreatePermissionAsync(
            //        new PermissionProperties(
            //            id: "permissionUser1Orders",
            //            permissionMode: PermissionMode.All,
            //            container: container,
            //            resourcePartitionKey: new PartitionKey("012345")));

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
                FeedResponse<TestClass> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (TestClass family in currentResultSet)
                {
                    //families.Add(family);
                    x++;
                }
            }
            return x;
            //MongoClient client = new MongoClient(ConnectionString);
            //var _database = client.GetDatabase(databaseId);
            ////_database.
            //var col =  _database.GetCollection<TestClass>(containerId);
            //return col.EstimatedDocumentCount();
        }

        //private static string GenerateMasterKeyAuthorizationSignature(string verb, string resourceId, string resourceType, string key, string keyType, string tokenVersion)
        //{
        //    var hmacSha256 = new System.Security.Cryptography.HMACSHA256 { Key = Convert.FromBase64String(key) };

        //    string payLoad = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}\n{1}\n{2}\n{3}\n{4}\n",
        //            verb.ToLowerInvariant(),
        //            resourceType.ToLowerInvariant(),
        //            resourceId,
        //            utc_date.ToLowerInvariant(),
        //            ""
        //    );

        //    byte[] hashPayLoad = hmacSha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payLoad));
        //    string signature = Convert.ToBase64String(hashPayLoad);

        //    return System.Web.HttpUtility.UrlEncode(String.Format(System.Globalization.CultureInfo.InvariantCulture, "type={0}&ver={1}&sig={2}",
        //        keyType,
        //        tokenVersion,
        //        signature));
        //}

        class TestClass
        {
            public string name { get; set; }

            public string id { get; set; }

            public string location { get; set; }
        }
    }
}
