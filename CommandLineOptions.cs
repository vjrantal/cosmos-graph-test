using CommandLine;

namespace cosmosdb_graph_test
{
    class CommandLineOptions
    {
        [Option('c', "connection-string", Required = true, HelpText = "Connection String")]
        public string ConnectionString { get; set; }
    }
}