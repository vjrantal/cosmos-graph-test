using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

/* 
    Documentation
    
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
        private static string accountKey = "";
        private static string apiKind = "";
        private static string database = "";
        private static string collection = "";


        private static Dictionary<string, string> gremlinQueries = new Dictionary<string, string>
        {
            { "Cleanup",        "g.V().drop()" },
            { "AddVertex 1",    "g.addV('person').property('id', 'thomas').property('firstName', 'Thomas').property('age', 44)" },
            { "AddVertex 2",    "g.addV('person').property('id', 'mary').property('firstName', 'Mary').property('lastName', 'Andersen').property('age', 39)" },
            { "AddVertex 3",    "g.addV('person').property('id', 'ben').property('firstName', 'Ben').property('lastName', 'Miller')" },
            { "AddVertex 4",    "g.addV('person').property('id', 'robin').property('firstName', 'Robin').property('lastName', 'Wakefield')" },
            { "AddEdge 1",      "g.V('thomas').addE('knows').to(g.V('mary'))" },
            { "AddEdge 2",      "g.V('thomas').addE('knows').to(g.V('ben'))" },
            { "AddEdge 3",      "g.V('ben').addE('knows').to(g.V('robin'))" },
            { "UpdateVertex",   "g.V('thomas').property('age', 44)" },
            { "CountVertices",  "g.V().count()" },
            { "Filter Range",   "g.V().hasLabel('person').has('age', gt(40))" },
            { "Project",        "g.V().hasLabel('person').values('firstName')" },
            { "Sort",           "g.V().hasLabel('person').order().by('firstName', decr)" },
            { "Traverse",       "g.V('thomas').out('knows').hasLabel('person')" },
            { "Traverse 2x",    "g.V('thomas').out('knows').hasLabel('person').out('knows').hasLabel('person')" },
            { "Loop",           "g.V('thomas').repeat(out()).until(has('id', 'robin')).path()" },
            { "DropEdge",       "g.V('thomas').outE('knows').where(inV().has('id', 'mary')).drop()" },
            { "CountEdges",     "g.E().count()" },
            { "DropVertex",     "g.V('thomas').drop()" },
        };

        static async Task Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
            if (result.Tag != ParserResultType.Parsed) return;

            unparsed_connection_string = ((Parsed<CommandLineOptions>)result).Value.ConnectionString;
            ParseUnparsedConnectionString(unparsed_connection_string);
            if(DoWeHaveAllParameters())
            {
                // Let's start
            }
            else
            {
                Console.WriteLine("Check the parameters");
            }

            // IConfiguration config = new ConfigurationBuilder()
            //     .AddEnvironmentVariables()
            //     .Build();

            // Console.WriteLine(config["cosmosdb_connectionstring"]);

            // var gremlinServer = new GremlinServer(hostname, port, enableSsl: true,
            //                                         username: "/dbs/" + database + "/colls/" + collection,
            //                                         password: authKey);

            // using (var gremlinClient = new GremlinClient(gremlinServer, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType))
            // {
            //     foreach (var query in gremlinQueries)
            //     {
            //         Console.WriteLine(String.Format("Running this query: {0}: {1}", query.Key, query.Value));

            //         // Create async task to execute the Gremlin query.
            //         var task = gremlinClient.SubmitAsync<dynamic>(query.Value);
            //         task.Wait();

            //         foreach (var result in task.Result)
            //         {
            //             // The vertex results are formed as Dictionaries with a nested dictionary for their properties
            //             string output = JsonConvert.SerializeObject(result);
            //             Console.WriteLine(String.Format("\tResult:\n\t{0}", output));
            //         }
            //         Console.WriteLine();
            //     }
            // }


            //InsertNode("1", 1).GetAwaiter().GetResult();
            Console.WriteLine("Finished");



        }

        private static bool DoWeHaveAllParameters()
        {
            // ApiKind needs to be Gremlin
            if(apiKind.ToLower() != "gremlin") return false;

            if(accountEndpoint != string.Empty && accountKey != string.Empty && database != string.Empty && collection != string.Empty)
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

        static async Task InsertNode(string parentId, int level)
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

            Console.WriteLine($"{padding} {parentId}");

            //InsertNodeInCosmos();

            for (int i = 0; i < 50; i++)
            {
                await InsertNode(parentId + "-" + i.ToString(), level + 1);
            }
        }

        static void InsertNodeInCosmos()
        {
            //Task.Delay(500).Wait();
        }

    }
}
