using ChanceNET;
using CommandLine;
using Microsoft.Azure.CosmosDB.BulkExecutor;
using Microsoft.Azure.CosmosDB.BulkExecutor.Graph.Element;
using Microsoft.Azure.Documents.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/* 
    Documentation
    
    Connection String
        Example: 
            AccountEndpoint=https://<cosmosdb-name>.documents.azure.com:443/;AccountKey=yh[...]==;ApiKind=Gremlin;Database=db01;Collection=col01
        Run in Code:
            Add -c <connection string> in the args collection in launch.json

    Third party components
        * CommandLineParser - https://github.com/commandlineparser/commandline
        * Chance - https://github.com/gmantaos/Chance.NET

 */
namespace cosmosdb_graph_test
{
    class Program
    {
        private static Random _random = new Random();
        private static Chance _chance = new Chance();

        private static string _unparsedConnectionString;
        private static string _rootNodeId = "";
        private static int _batchSize;

        private static string _accountEndpoint = "";
        private const int _port = 443;
        private static string _accountKey = "";
        private static string _apiKind = "";
        private static string _database = "";
        private static string _collection = "";

        private static DocumentClient _documentClient;
        private static readonly ConnectionPolicy _connectionPolicy = new ConnectionPolicy
        {
            ConnectionMode = ConnectionMode.Direct,
            ConnectionProtocol = Protocol.Tcp
        };
        private static BulkExecutor _bulkExecutor;
        private static readonly IList<object> _verticesAndEdgesToAdd = new List<object>();

        private static async Task Main(string[] args)
        {
            var result = (Parsed<CommandLineOptions>)Parser.Default.ParseArguments<CommandLineOptions>(args);
            if (result.Tag != ParserResultType.Parsed)
                return;

            _unparsedConnectionString = result.Value.ConnectionString;
            _rootNodeId = result.Value.RootNode.Trim();
            _batchSize = result.Value.BatchSize;

            ParseConnectionString(_unparsedConnectionString);            

            if (DoWeHaveAllParameters())
            {
                await InitializeCosmosDbAsync();
                await InsertNodeAsync(_rootNodeId, "", "", 1);

                // Import remaining vertices and edges
                await BulkImportToCosmosDbAsync();
            }
            else
            {
                Console.WriteLine("Check the parameters");
            }

            Console.WriteLine("Finished");
        }

        private static async Task InitializeCosmosDbAsync()
        {
            _documentClient = new DocumentClient(new Uri(_accountEndpoint), _accountKey, _connectionPolicy);
            var dataCollection = _documentClient.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(_database))
                .Where(c => c.Id == _collection).AsEnumerable().FirstOrDefault();

            // Set retry options high during initialization (default values).
            _documentClient.ConnectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = 30;
            _documentClient.ConnectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = 9;

            _bulkExecutor = new BulkExecutor(_documentClient, dataCollection);
            await _bulkExecutor.InitializeAsync();

            // Set retries to 0 to pass complete control to bulk executor.
            _documentClient.ConnectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = 0;
            _documentClient.ConnectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = 0;
        }

        private static bool DoWeHaveAllParameters()
        {
            // ApiKind needs to be Gremlin
            if (_apiKind.ToLower() != "gremlin")
                return false;

            if (_accountEndpoint != string.Empty && _accountKey != string.Empty && 
                _database != string.Empty && _collection != string.Empty)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void ParseConnectionString(string unparsedConnectionString)
        {
            foreach (var part in unparsedConnectionString.Trim().Split(';'))
            {
                switch (GetKeyFromPart(part.ToLower()))
                {
                    case "accountendpoint":
                        _accountEndpoint = GetValueFromPart(part);
                        break;
                    case "accountkey":
                        _accountKey = GetValueFromPart(part);
                        break;
                    case "apikind":
                        _apiKind = GetValueFromPart(part);
                        break;
                    case "database":
                        _database = GetValueFromPart(part);
                        break;
                    case "collection":
                        _collection = GetValueFromPart(part);
                        break;
                    default:
                        break;
                }
            }
        }

        private static string GetValueFromPart(string part)
        {
            return part.Split(new[] { '=' }, 2)[1];
        }

        private static string GetKeyFromPart(string part)
        {
            return part.Split(new[] { '=' }, 2)[0];
        }

        private static async Task InsertNodeAsync(string id, string parentId, string parentLabel, int level)
        {
            var numberOfNodesToCreate = 0;
            var properties = new Dictionary<string, object>();
            var label = "node";

            if (level == 6)
                return;

            switch (level)
            {
                case 1:
                    numberOfNodesToCreate = _random.Next(1, 10);
                    break;
                case 2:
                    numberOfNodesToCreate = _random.Next(1, 100);
                    break;
                case 3:
                    numberOfNodesToCreate = _random.Next(1, 40);
                    break;
                case 4:
                    numberOfNodesToCreate = _random.Next(1, 20);
                    label = "asset";
                    properties = new Dictionary<string, object>() {
                        {"manufacturer", _chance.PickOne(new string[] {"siemens", "abb", "vortex", "mulvo", "ropert"})},
                        {"installedAt", _chance.Timestamp()},
                        {"serial", _chance.Guid().ToString()},
                        {"comments", _chance.Sentence(30)}                          
                    };
                    break;
                case 5:
                    numberOfNodesToCreate = _random.Next(1, 20);
                    label = "asset";
                    properties = new Dictionary<string, object>() {
                        {"manufacturer", _chance.PickOne(new string[] {"siemens", "abb", "vortex", "mulvo", "ropert"})},
                        {"installedAt", _chance.Timestamp()},
                        {"serial", _chance.Guid().ToString()},
                        {"comments", _chance.Sentence(30)}                          
                    };
                    break;
            }

            properties.Add("partitionId", $"{_rootNodeId}");
            properties.Add("level", level);
            properties.Add("createdAt", DateTimeOffset.Now.ToUnixTimeMilliseconds());
            properties.Add("name", id);
            properties.Add("parentId", parentId);

            var padding = new StringBuilder().Append('-', level).ToString();
            Console.WriteLine($"{padding} {id}");

            var vertex = CreateGremlinVertex(id, label, properties);
            await BulkInsertAsync(vertex);

            if (parentId != string.Empty)
            {
                var edge = CreateGremlinEdge(parentId, id, parentLabel, label);
                await BulkInsertAsync(edge);
            }

            for (var i = 0; i < numberOfNodesToCreate; i++)
            {
                await InsertNodeAsync(id + "-" + i.ToString(), id, label, level + 1);
            }
        }

        private static GremlinVertex CreateGremlinVertex(string id, string label, 
            Dictionary<string, object> properties)
        {
            var vertex = new GremlinVertex(id, label);

            foreach (var property in properties)
            {
                vertex.AddProperty(property.Key, property.Value);
            }

            return vertex;
        }

        private static GremlinEdge CreateGremlinEdge(string sourceId, string destinationId, 
            string sourceLabel, string destinationLabel)
        {
            var edgeId = $"{sourceId} -> {destinationId}";
            var edge = new GremlinEdge(edgeId, "child", sourceId, destinationId, 
                sourceLabel, destinationLabel);

            edge.AddProperty("model", "primary");
            return edge;
        }

        private static async Task BulkInsertAsync(object vertexOrEdge)
        {
            _verticesAndEdgesToAdd.Add(vertexOrEdge);
            if (_verticesAndEdgesToAdd.Count >= _batchSize)
            {
                await BulkImportToCosmosDbAsync();                
            }
        }

        private static async Task BulkImportToCosmosDbAsync()
        {
            var response = await _bulkExecutor.BulkImportAsync(_verticesAndEdgesToAdd);

            if (response.BadInputDocuments.Any())
                throw new Exception("BulkExecutor found bad input vertices and edges!");

            _verticesAndEdgesToAdd.Clear();
        }
    }
}