using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Gremlin.Net.Structure;
using Gremlin.Net.Driver.Remote;
using Polly.Retry;
using Polly;
using Gremlin.Net.Driver.Exceptions;
using ChanceNET;

/* 
    Documentation
    
    Connection String
        Example: 
            AccountEndpoint=https://<cosmosdb-name>.gremlin.cosmosdb.azure.com:443/;AccountKey=yh[...]==;ApiKind=Gremlin;Database=db01;Collection=col01
        Run in Code:
            Add -c <connection string> in the args collection in launch.json

    Third party components
        * CommandLineParser - https://github.com/commandlineparser/commandline
        * Polly - https://github.com/App-vNext/Polly
        * Chance - https://github.com/gmantaos/Chance.NET

 */
namespace cosmosdb_graph_test
{
    class Program
    {
        // remove
        static private Random random = new Random();
        static private Chance chance = new Chance();

        private static string unparsed_connection_string;
        private static string accountEndpoint = "";
        private static int port = 443;
        private static string accountKey = "";
        private static string apiKind = "";
        private static string database = "";
        private static string collection = "";
        private static string rootNodeId = "";
        private static GremlinClient gremlinClient;
        private static RetryPolicy retryWithWait;

        static async Task Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
            if (result.Tag != ParserResultType.Parsed) return;

            unparsed_connection_string = ((Parsed<CommandLineOptions>)result).Value.ConnectionString;
            rootNodeId = ((Parsed<CommandLineOptions>)result).Value.RootNode.Trim();

            ParseUnparsedConnectionString(unparsed_connection_string);
            if (DoWeHaveAllParameters())
            {
                retryWithWait = Policy
                    .Handle<ResponseException>(r => r.Message.ToLower().Contains("request rate is large"))
                    .WaitAndRetryForever(retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(random.Next(0, 1000)));

                // Let's start
                var gremlinServer = new GremlinServer(accountEndpoint, port, enableSsl: true,
                                                        username: "/dbs/" + database + "/colls/" + collection,
                                                        password: accountKey);

                gremlinClient = new GremlinClient(gremlinServer, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType);

                InsertNode(rootNodeId, "", 1).GetAwaiter().GetResult();

                gremlinClient.Dispose();
            }
            else
            {
                Console.WriteLine("Check the parameters");
            }

            Console.WriteLine("Finished");
        }

        private static bool DoWeHaveAllParameters()
        {
            // ApiKind needs to be Gremlin
            if (apiKind.ToLower() != "gremlin") return false;

            if (accountEndpoint != string.Empty && accountKey != string.Empty && database != string.Empty && collection != string.Empty)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void ParseUnparsedConnectionString(string unparsedConnectionString)
        {
            foreach (var part in unparsedConnectionString.Trim().Split(';'))
            {
                switch (GetKeyFromPart(part.ToLower()))
                {
                    case "accountendpoint":
                        accountEndpoint = GetValueFromPart(part);
                        // we need to strip out some part of it
                        accountEndpoint = Regex.Replace(accountEndpoint, "https://", "");
                        accountEndpoint = Regex.Replace(accountEndpoint, @":443(\/)?", "");
                        break;
                    case "accountkey":
                        accountKey = GetValueFromPart(part);
                        break;
                    case "apikind":
                        apiKind = GetValueFromPart(part);
                        break;
                    case "database":
                        database = GetValueFromPart(part);
                        break;
                    case "collection":
                        collection = GetValueFromPart(part);
                        break;
                    default:
                        break;
                }
            }
        }

        private static string GetValueFromPart(string part)
        {
            return part.Split('=', 2)[1];
        }

        private static string GetKeyFromPart(string part)
        {
            return part.Split('=', 2)[0];
        }

        static async Task InsertNode(string id, string parentId, int level)
        {
            int numberOfNodesToCreate = 0;
            Dictionary<string, object> properties = new Dictionary<string, object>();
            string label = "node";

            if (level == 6) return;

            switch (level)
            {
                case 1:
                    numberOfNodesToCreate = random.Next(1, 10);
                    break;
                case 2:
                    numberOfNodesToCreate = random.Next(1, 100);
                    break;
                case 3:
                    numberOfNodesToCreate = random.Next(1, 40);
                    break;
                case 4:
                    numberOfNodesToCreate = random.Next(1, 20);
                    label = "asset";
                    properties = new Dictionary<string, object>() {
                        {"manufactor", chance.PickOne(new string[] {"siemens", "abb", "vortex", "mulvo", "ropert"})},
                        {"installedAt", chance.Timestamp()},
                        {"serial", chance.Guid().ToString()},
                        {"comments", chance.Sentence(30)}                          
                    };
                    break;
                case 5:
                    numberOfNodesToCreate = random.Next(1, 20);
                    label = "asset";
                    properties = new Dictionary<string, object>() {
                        {"manufactor", chance.PickOne(new string[] {"siemens", "abb", "vortex", "mulvo", "ropert"})},
                        {"installedAt", chance.Timestamp()},
                        {"serial", chance.Guid().ToString()},
                        {"comments", chance.Sentence(30)}                          
                    };
                    break;
            }

            properties.Add("partitionId", $"{rootNodeId}");
            properties.Add("level", level);
            properties.Add("createdAt", DateTimeOffset.Now.ToUnixTimeMilliseconds());
            properties.Add("name", id);
            properties.Add("parentId", parentId);

            string padding = new StringBuilder().Append('-', level).ToString();

            Console.WriteLine($"{padding} {id}");

            var gremlinStatement = CreateGremlinStatementToCreateAVertex(id, label, properties);

            InsertNodeInCosmos(gremlinStatement);

            if (parentId != string.Empty) InsertEdgeInCosmos(parentId, id);

            for (int i = 0; i < numberOfNodesToCreate; i++)
            {
                await InsertNode(id + "-" + i.ToString(), id, level + 1);
            }
        }

        private static void InsertEdgeInCosmos(string parentId, string id)
        {
            retryWithWait.Execute(() => gremlinClient.SubmitAsync<dynamic>(CreateGremlinStatementToCreateAnEdge(parentId, id, "child")).GetAwaiter().GetResult());
        }

        private static string CreateGremlinStatementToCreateAVertex(string id, string label, Dictionary<string, object> properties)
        {
            const string template = "g.addV('{0}').property('id', '{1}')";
            string propertyTemplate = "";

            StringBuilder sb = new StringBuilder(string.Format(template, label, id));

            foreach (var property in properties)
            {
                if (property.Value is string)
                {
                    propertyTemplate = ".property('{0}', '{1}')";
                }
                else
                {
                    propertyTemplate = ".property('{0}', {1})";
                }

                sb.Append(string.Format(propertyTemplate, $"{property.Key}", $"{property.Value}"));
            }

            return sb.ToString();
        }

        private static string CreateGremlinStatementToCreateAnEdge(string sourceId, string destinationId, string label)
        {
            // const string template = "g.V('{0}').addE('{1}').property('model','primary').to(g.V('{2}')).V('{2}').addE('root').to(g.V('{3}'))";
            const string template = "g.V('{0}').addE('{1}').property('model','primary').to(g.V('{2}'))";

            return string.Format(template, sourceId, label, destinationId, rootNodeId);
        }

        private static void InsertNodeInCosmos(string gremlingStatement)
        {
            retryWithWait.Execute(() => gremlinClient.SubmitAsync<dynamic>(gremlingStatement).GetAwaiter().GetResult());
        }
    }
}
