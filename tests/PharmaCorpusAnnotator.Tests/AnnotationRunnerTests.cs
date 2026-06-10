using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using PharmaCorpusAnnotator.Core.Csv;
using PharmaCorpusAnnotator.Core.Interfaces;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Output;
using PharmaCorpusAnnotator.Core.Pipeline;
using PharmaCorpusAnnotator.Core.Tokenization;

namespace PharmaCorpusAnnotator.Tests;

public sealed class AnnotationRunnerTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerOptions.Web);
    private readonly string _tmpDir;

    public AnnotationRunnerTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "pharma-runner-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tmpDir);
    }

    [Fact]
    public async Task Resume_MaxRows_LimitsNewRows()
    {
        var input = Path.Combine(_tmpDir, "source.csv");
        await File.WriteAllTextAsync(
            input,
            "Código Nacional;Nombre del producto farmacéutico" + Environment.NewLine +
            "140001;producto uno 1 mg" + Environment.NewLine +
            "140002;producto dos 2 mg" + Environment.NewLine +
            "140003;producto tres 3 mg" + Environment.NewLine +
            "140004;producto cuatro 4 mg" + Environment.NewLine +
            "140005;producto cinco 5 mg" + Environment.NewLine,
            TestContext.Current.CancellationToken);

        var output = Path.Combine(_tmpDir, "corpus.json");
        var failedOutput = Path.Combine(_tmpDir, "failed.jsonl");
        var runner = CreateRunner();

        await runner.RunAsync(CreateOptions(input, output, failedOutput, maxRows: 2), TestContext.Current.CancellationToken);
        await runner.RunAsync(CreateOptions(input, output, failedOutput, maxRows: 2), TestContext.Current.CancellationToken);

        using var f = File.OpenRead(output);
        var doc = JsonSerializer.Deserialize<PharmaCorpusDocument>(f, JsonOpts)!;

        doc.Sources.Should().HaveCount(1);
        doc.Sources[0].Records.Select(r => r.RowNumber).Should().Equal(2, 3, 4, 5);
    }

    private static AnnotationRunner CreateRunner()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        return new AnnotationRunner(
            new CsvPharmaSourceReader(loggerFactory.CreateLogger<CsvPharmaSourceReader>()),
            new PharmaTokenizer(),
            new SuccessfulModelClient(),
            loggerFactory);
    }

    private static AnnotationRunnerOptions CreateOptions(
        string input,
        string output,
        string failedOutput,
        int maxRows) =>
        new(
            new CsvSourceOptions(
                input,
                "test-source",
                "Nombre del producto farmacéutico",
                ";",
                "utf-8-sig",
                ["Código Nacional"],
                0,
                maxRows),
            output,
            failedOutput,
            Resume: true,
            DryRun: false,
            Verbose: false);

    public void Dispose()
    {
        try { Directory.Delete(_tmpDir, recursive: true); }
        catch { /* best effort */ }
    }

    private sealed class SuccessfulModelClient : IPharmaAnnotationModelClient
    {
        public Task<PharmaAnnotationResponse> AnnotateAsync(
            PharmaAnnotationModelRequest request,
            CancellationToken cancellationToken = default)
        {
            var tokens = request.Tokens
                .Select(t => new AnnotatedToken(t.Index, t.Text, "O", null, 1.0))
                .ToList();

            var response = new PharmaAnnotationResponse(
                tokens,
                new NormalizedPharmaItem(null, null, null, [], null, null, null, null, null, null, null, null),
                new AnnotationQuality(null, false, []));

            return Task.FromResult(response);
        }
    }
}
