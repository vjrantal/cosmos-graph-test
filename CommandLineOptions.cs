using CommandLine;

namespace cosmosdb_graph_test
{
    class CommandLineOptions
    {
        [Option('c', "connection-string", Required = true, HelpText = "Connection String")]
        public string ConnectionString { get; set; }

        [Option('r', "rootname", Required = false, HelpText = "Name of root node", Default = "1")]
        public string rootNode { get; set; }
    }
}