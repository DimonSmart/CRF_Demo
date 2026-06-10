using Microsoft.Extensions.Logging;
using PharmaCorpusAnnotator.Cli;

if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
{
    CliHelp.Print();
    return 0;
}

if (args[0] == "annotate")
{
    return await AnnotateCommand.RunAsync(args[1..]);
}

Console.Error.WriteLine($"Unknown command '{args[0]}'. Use 'annotate' or --help.");
return 1;
