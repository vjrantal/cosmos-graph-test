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

 */
namespace cosmosdb_graph_test
{
    class Program
    {
        // remove
        static private Random random = new Random();

        private static string unparsed_connection_string;
        private static string accountEndpoint = "";
        private static int port = 443;
        private static string accountKey = "";
        private static string apiKind = "";
        private static string database = "";
        private static string collection = "";
        private static GremlinClient gremlinClient;
        private static RetryPolicy retryWithWait;

        static async Task Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
            if (result.Tag != ParserResultType.Parsed) return;

            unparsed_connection_string = ((Parsed<CommandLineOptions>)result).Value.ConnectionString;
            var rootNodeName = ((Parsed<CommandLineOptions>)result).Value.RootNode.Trim();

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

                InsertNode(rootNodeName, "", 1).GetAwaiter().GetResult();

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

            if (level == 7) return;

            switch (level)
            {
                case 1:
                    numberOfNodesToCreate = 45;
                    break;
                case 2:
                    numberOfNodesToCreate = random.Next(1, 10);
                    break;
                case 3:
                    numberOfNodesToCreate = random.Next(1, 100);
                    break;
                case 4:
                    numberOfNodesToCreate = random.Next(1, 40);
                    break;
                case 5:
                    numberOfNodesToCreate = random.Next(1, 20);
                    break;
                case 6:
                    numberOfNodesToCreate = random.Next(1, 20);
                    break;
                default:
                    numberOfNodesToCreate = 0;
                    break;
            }

            string padding = new StringBuilder().Append('-', level).ToString();

            Console.WriteLine($"{padding} {id}");

            InsertNodeInCosmos(id);

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

        private static string CreateGremlinStatementToCreateAVertex(string id, int numberOfProperties = 20)
        {
            const string template = "g.addV('asset').property('id', '{0}')";
            const string propertyTemplate = ".property('{0}', '{1}')";
            StringBuilder sb = new StringBuilder(string.Format(template, id));

            for (int i = 0; i < numberOfProperties; i++)
            {
                sb.Append(string.Format(propertyTemplate, $"prop{i}", $"value{i}"));
            }

            return sb.ToString();
        }

        private static string CreateGremlinStatementToCreateAnEdge(string sourceId, string destinationId, string label)
        {
            const string template = "g.V('{0}').addE('{1}').to(g.V('{2}'))";

            return string.Format(template, sourceId, label, destinationId);
        }

        private static void InsertNodeInCosmos(string id)
        {
            retryWithWait.Execute(() => gremlinClient.SubmitAsync<dynamic>(CreateGremlinStatementToCreateAVertex(id)).GetAwaiter().GetResult());
        }
    }
}
