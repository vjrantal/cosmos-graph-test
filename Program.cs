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

/* 
    Documentation
    
    Connection String
        Example: 
            AccountEndpoint=https://<cosmosdb-name>.gremlin.cosmosdb.azure.com:443/;AccountKey=yh[...]==;ApiKind=Gremlin;Database=db01;Collection=col01
        Run in Code:
            Add -c <connection string> in the args collection in launch.json
            
    Third party components
        * Chance - https://github.com/gmantaos/Chance.NET
        * CommandLineParser - https://github.com/commandlineparser/commandline



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

        // private static Dictionary<string, string> gremlinQueries = new Dictionary<string, string>
        // {
        //     { "Cleanup",        "g.V().drop()" },
        //     { "AddVertex 1",    "g.addV('person').property('id', 'thomas').property('firstName', 'Thomas').property('age', 44)" },
        //     { "AddVertex 2",    "g.addV('person').property('id', 'mary').property('firstName', 'Mary').property('lastName', 'Andersen').property('age', 39)" },
        //     { "AddVertex 3",    "g.addV('person').property('id', 'ben').property('firstName', 'Ben').property('lastName', 'Miller')" },
        //     { "AddVertex 4",    "g.addV('person').property('id', 'robin').property('firstName', 'Robin').property('lastName', 'Wakefield')" },
        //     { "AddEdge 1",      "g.V('thomas').addE('knows').to(g.V('mary'))" },
        //     { "AddEdge 2",      "g.V('thomas').addE('knows').to(g.V('ben'))" },
        //     { "AddEdge 3",      "g.V('ben').addE('knows').to(g.V('robin'))" },
        //     { "UpdateVertex",   "g.V('thomas').property('age', 44)" },
        //     { "CountVertices",  "g.V().count()" },
        //     { "Filter Range",   "g.V().hasLabel('person').has('age', gt(40))" },
        //     { "Project",        "g.V().hasLabel('person').values('firstName')" },
        //     { "Sort",           "g.V().hasLabel('person').order().by('firstName', decr)" },
        //     { "Traverse",       "g.V('thomas').out('knows').hasLabel('person')" },
        //     { "Traverse 2x",    "g.V('thomas').out('knows').hasLabel('person').out('knows').hasLabel('person')" },
        //     { "Loop",           "g.V('thomas').repeat(out()).until(has('id', 'robin')).path()" },
        //     { "DropEdge",       "g.V('thomas').outE('knows').where(inV().has('id', 'mary')).drop()" },
        //     { "CountEdges",     "g.E().count()" },
        //     { "DropVertex",     "g.V('thomas').drop()" },
        // };

        static async Task Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
            if (result.Tag != ParserResultType.Parsed) return;

            unparsed_connection_string = ((Parsed<CommandLineOptions>)result).Value.ConnectionString;
            ParseUnparsedConnectionString(unparsed_connection_string);
            if (DoWeHaveAllParameters())
            {
                // Let's start
                var gremlinServer = new GremlinServer(accountEndpoint, port, enableSsl: true,
                                                        username: "/dbs/" + database + "/colls/" + collection,
                                                        password: accountKey);


                // this doesn't work in CosmosDB
                // var graph = new Graph();
                // var g = graph.Traversal().WithRemote(new DriverRemoteConnection(new GremlinClient(gremlinServer)));
                // var vertex = g.AddV("person").Property("name", "kalle");

                gremlinClient = new GremlinClient(gremlinServer, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType);
                //var resultFromSubmit = gremlinClient.SubmitAsync<dynamic>("g.addV('person').property('id', 'thomas').property('firstName', 'Thomas').property('age', 44)").GetAwaiter().GetResult();
                
                 InsertNode("1", 1).GetAwaiter().GetResult();
                
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

        static async Task InsertNode(string id, int level)
        {
            int numberOfNodesToCreate = 0;

            if (level == 6) return;

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
                default:
                    numberOfNodesToCreate = 0;
                    break;
            }

            string padding = new StringBuilder().Append('-', level).ToString();

            Console.WriteLine($"{padding} {id}");

            InsertNodeInCosmos(id);

            for (int i = 0; i < numberOfNodesToCreate; i++)
            {
                await InsertNode(id + "-" + i.ToString(), level + 1);
            }
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

        private static void InsertNodeInCosmos(string id)
        {
            var resultFromSubmit = gremlinClient.SubmitAsync<dynamic>(CreateGremlinStatementToCreateAVertex(id)).GetAwaiter().GetResult();
        }

    }
}
