using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using PharmaCorpusAnnotator.Core.Csv;
using PharmaCorpusAnnotator.Core.Llm;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Pipeline;
using PharmaCorpusAnnotator.Core.Tokenization;

namespace PharmaCorpusAnnotator.Cli;

public static class AnnotateCommand
{
    private const string DefaultTextColumn = "Nombre del producto farmacéutico";

    public static async Task<int> RunAsync(string[] args)
    {
        var parsed = ArgParser.Parse(args);

        if (parsed.ContainsKey("--context-columns"))
        {
            Console.Error.WriteLine("Error: --context-columns is no longer supported.");
            return 1;
        }

        // Required
        if (!parsed.TryGetValue("--input", out var input) || string.IsNullOrEmpty(input))
        {
            Console.Error.WriteLine("Error: --input is required.");
            return 1;
        }

        if (!parsed.TryGetValue("--output", out var output) || string.IsNullOrEmpty(output))
        {
            Console.Error.WriteLine("Error: --output is required.");
            return 1;
        }

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"Error: Input file not found: {input}");
            return 1;
        }

        var textColumn = parsed.GetValueOrDefault("--text-column", DefaultTextColumn)!;

        var sourceKey = parsed.GetValueOrDefault("--source-key")
            ?? SlugFromFileName(input);

        var delimiter = parsed.GetValueOrDefault("--delimiter", ";")!;
        var encoding = parsed.GetValueOrDefault("--encoding", "utf-8-sig")!;

        int? maxRows = parsed.TryGetValue("--max-rows", out var mrStr)
            && int.TryParse(mrStr, out var mr) ? mr : null;

        int skip = parsed.TryGetValue("--skip", out var skipStr)
            && int.TryParse(skipStr, out var sk) ? sk : 0;

        bool resume = !parsed.ContainsKey("--no-resume");
        bool verbose = parsed.ContainsKey("--verbose");
        bool dryRun = parsed.ContainsKey("--dry-run");

        var failedOutput = parsed.GetValueOrDefault("--failed-output")
            ?? DeriveFailedPath(output);
        var attemptsOutput = parsed.GetValueOrDefault("--attempts-output");

        var logLevel = verbose ? LogLevel.Debug : LogLevel.Information;
        using var loggerFactory = LoggerFactory.Create(b =>
            b.SetMinimumLevel(logLevel)
                .AddFilter("Microsoft.Agents.AI", LogLevel.Warning)
                .AddFilter("Microsoft.Extensions.AI", LogLevel.Warning)
                .AddConsole(o => o.FormatterName = TimestampConsoleFormatter.FormatterName)
                .AddConsoleFormatter<TimestampConsoleFormatter, ConsoleFormatterOptions>());

        var csvOpts = new CsvSourceOptions(
            InputPath: input,
            SourceKey: sourceKey,
            TextColumn: textColumn,
            Delimiter: delimiter,
            Encoding: encoding,
            Skip: skip,
            MaxRows: maxRows);

        var runnerOpts = new AnnotationRunnerOptions(
            CsvOptions: csvOpts,
            OutputPath: output,
            FailedOutputPath: failedOutput,
            Resume: resume,
            DryRun: dryRun,
            Verbose: verbose);

        if (!dryRun)
        {
            var llmOpts = LlmOptionsFactory.FromEnvironment(attemptsOutput);
            loggerFactory.CreateLogger("Config").LogInformation(
                "LLM: {Model} @ {Endpoint}", llmOpts.ModelId, llmOpts.BaseEndpoint);

            var modelClient = PharmaModelClientFactory.Create(llmOpts, loggerFactory);
            var reader = new CsvPharmaSourceReader(loggerFactory.CreateLogger<CsvPharmaSourceReader>());
            var tokenizer = new PharmaTokenizer();
            var runner = new AnnotationRunner(reader, tokenizer, modelClient, loggerFactory);

            await runner.RunAsync(runnerOpts);
        }
        else
        {
            var reader = new CsvPharmaSourceReader(loggerFactory.CreateLogger<CsvPharmaSourceReader>());
            var tokenizer = new PharmaTokenizer();
            // Dry-run: no LLM client needed; pass null-object
            var nullClient = new NullModelClient();
            var runner = new AnnotationRunner(reader, tokenizer, nullClient, loggerFactory);
            await runner.RunAsync(runnerOpts);
        }

        return 0;
    }

    private static string SlugFromFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return System.Text.RegularExpressions.Regex.Replace(name, @"[^a-z0-9]+", "-").Trim('-');
    }

    private static string DeriveFailedPath(string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(outputPath);
        return Path.Combine(dir, name + ".failed.jsonl");
    }
}
