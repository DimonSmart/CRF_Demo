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
    private const string RequiredColumn = "Principio activo o asociación de principios activos";
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

        var json = await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken);
        json.Should().NotContain("\"context\"");
        json.Should().NotContain("\"contextColumns\"");
        json.Should().NotContain("\"needsReview\"");
    }

    [Fact]
    public async Task FailedRecords_DoNotContainContext()
    {
        var input = Path.Combine(_tmpDir, "source.csv");
        await File.WriteAllTextAsync(
            input,
            "Código Nacional;Nombre del producto farmacéutico" + Environment.NewLine +
            "140001;producto uno 1 mg" + Environment.NewLine,
            TestContext.Current.CancellationToken);

        var output = Path.Combine(_tmpDir, "corpus.json");
        var failedOutput = Path.Combine(_tmpDir, "failed.jsonl");
        var runner = CreateRunner(new FailingModelClient());

        await runner.RunAsync(CreateOptions(input, output, failedOutput, maxRows: 1), TestContext.Current.CancellationToken);

        var jsonl = await File.ReadAllTextAsync(failedOutput, TestContext.Current.CancellationToken);
        jsonl.Should().NotContain("\"context\"");
        jsonl.Should().Contain("\"error\"");
    }

    [Fact]
    public async Task RequiredNonEmptyColumn_IsStoredInSourceMetadata()
    {
        var input = Path.Combine(_tmpDir, "source.csv");
        await File.WriteAllTextAsync(
            input,
            $"Código Nacional;Nombre del producto farmacéutico;{RequiredColumn}" + Environment.NewLine +
            "140001;producto uno 1 mg;ACTIVO" + Environment.NewLine +
            "140002;producto dos 2 mg;" + Environment.NewLine,
            TestContext.Current.CancellationToken);

        var output = Path.Combine(_tmpDir, "corpus.json");
        var failedOutput = Path.Combine(_tmpDir, "failed.jsonl");
        var runner = CreateRunner();

        await runner.RunAsync(
            CreateOptions(input, output, failedOutput, maxRows: 10, requiredNonEmptyColumn: RequiredColumn),
            TestContext.Current.CancellationToken);

        using var f = File.OpenRead(output);
        var doc = JsonSerializer.Deserialize<PharmaCorpusDocument>(f, JsonOpts)!;

        doc.Sources[0].Source.RequiredNonEmptyColumn.Should().Be(RequiredColumn);
        doc.Sources[0].Records.Select(r => r.RowNumber).Should().Equal(2);
    }

    [Fact]
    public async Task RequiredNonEmptyColumn_IsNotSentToModelRequest()
    {
        var input = Path.Combine(_tmpDir, "source.csv");
        await File.WriteAllTextAsync(
            input,
            $"Código Nacional;Nombre del producto farmacéutico;{RequiredColumn}" + Environment.NewLine +
            "140001;producto uno 1 mg;ACTIVO" + Environment.NewLine,
            TestContext.Current.CancellationToken);

        var output = Path.Combine(_tmpDir, "corpus.json");
        var failedOutput = Path.Combine(_tmpDir, "failed.jsonl");
        var modelClient = new CapturingModelClient();
        var runner = CreateRunner(modelClient);

        await runner.RunAsync(
            CreateOptions(input, output, failedOutput, maxRows: 1, requiredNonEmptyColumn: RequiredColumn),
            TestContext.Current.CancellationToken);

        modelClient.Requests.Should().ContainSingle();
        modelClient.Requests[0].Text.Should().Be("producto uno 1 mg");
        modelClient.Requests[0].Tokens.Select(t => t.Text).Should().Equal("producto", "uno", "1", "mg");
    }

    private static AnnotationRunner CreateRunner(IPharmaAnnotationModelClient? modelClient = null)
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        return new AnnotationRunner(
            new CsvPharmaSourceReader(loggerFactory.CreateLogger<CsvPharmaSourceReader>()),
            new PharmaTokenizer(),
            modelClient ?? new SuccessfulModelClient(),
            loggerFactory);
    }

    private static AnnotationRunnerOptions CreateOptions(
        string input,
        string output,
        string failedOutput,
        int maxRows,
        string? requiredNonEmptyColumn = null) =>
        new(
            new CsvSourceOptions(
                input,
                "test-source",
                "Nombre del producto farmacéutico",
                ";",
                "utf-8-sig",
                0,
                maxRows,
                requiredNonEmptyColumn),
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
                new AnnotationQuality(null, []));

            return Task.FromResult(response);
        }
    }

    private sealed class FailingModelClient : IPharmaAnnotationModelClient
    {
        public Task<PharmaAnnotationResponse> AnnotateAsync(
            PharmaAnnotationModelRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("test failure");
    }

    private sealed class CapturingModelClient : IPharmaAnnotationModelClient
    {
        public List<PharmaAnnotationModelRequest> Requests { get; } = [];

        public Task<PharmaAnnotationResponse> AnnotateAsync(
            PharmaAnnotationModelRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var tokens = request.Tokens
                .Select(t => new AnnotatedToken(t.Index, t.Text, "O", null, 1.0))
                .ToList();

            var response = new PharmaAnnotationResponse(
                tokens,
                new NormalizedPharmaItem(null, null, null, [], null, null, null, null, null, null, null, null),
                new AnnotationQuality(null, []));

            return Task.FromResult(response);
        }
    }
}
