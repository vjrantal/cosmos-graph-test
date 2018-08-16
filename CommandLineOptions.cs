using CommandLine;

namespace cosmosdb_graph_test
{
    class CommandLineOptions
    {
        [Option('c', "connection-string", Required = true, HelpText = "Connection String")]
        public string ConnectionString { get; set; }

        [Option('r', "rootname", Required = false, HelpText = "Name of root node", Default = "1")]
        public string RootNode { get; set; }

        [Option('b', "batch-size", Required = false, HelpText = "Batch size", Default = 100)]
        public int BatchSize { get; set; }
    }
}