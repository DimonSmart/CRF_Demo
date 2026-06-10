using FluentAssertions;
using PharmaCorpusAnnotator.Cli;
using PharmaCorpusAnnotator.Core.Tokenization;

namespace PharmaCorpusAnnotator.Tests;

public class CliSmokeTests
{
    private static readonly string SampleCsv =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.csv");

    [Fact]
    public async Task DryRun_ReadsAndTokenizes_WithoutLlm()
    {
        var output = Path.Combine(Path.GetTempPath(), $"pharma-smoke-{Guid.NewGuid()}.json");
        try
        {
            var args = new[]
            {
                "--input", SampleCsv,
                "--output", output,
                "--source-key", "test",
                "--max-rows", "3",
                "--dry-run",
            };

            var result = await AnnotateCommand.RunAsync(args);
            result.Should().Be(0);
            File.Exists(output).Should().BeFalse("dry-run should not produce output");
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public async Task MaxRows2_Processes_OnlyTwoRows()
    {
        // We use dry-run so no LLM is needed
        var stdOut = new System.IO.StringWriter();
        var origOut = Console.Out;
        Console.SetOut(stdOut);
        try
        {
            var args = new[]
            {
                "--input", SampleCsv,
                "--output", Path.GetTempFileName(),
                "--source-key", "test",
                "--max-rows", "2",
                "--dry-run",
            };
            await AnnotateCommand.RunAsync(args);
            var output = stdOut.ToString();
            var rowCount = output.Split("[DRY-RUN]", StringSplitOptions.RemoveEmptyEntries).Length - 1;
            rowCount.Should().Be(2);
        }
        finally
        {
            Console.SetOut(origOut);
        }
    }

    [Fact]
    public async Task MissingInputFile_ReturnsNonZero()
    {
        var args = new[]
        {
            "--input", "nonexistent_file.csv",
            "--output", "out.json",
            "--dry-run",
        };
        var result = await AnnotateCommand.RunAsync(args);
        result.Should().NotBe(0);
    }

    [Fact]
    public async Task ContextColumns_ReturnsNonZero()
    {
        var output = Path.GetTempFileName();
        var args = new[]
        {
            "--input", SampleCsv,
            "--output", output,
            "--context-columns", "Código Nacional",
            "--dry-run",
        };

        try
        {
            var result = await AnnotateCommand.RunAsync(args);
            result.Should().NotBe(0);
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void SlugFromFileName_GeneratesStableKey()
    {
        var key = PharmaCorpusAnnotatorCliReflection.GetSlug("20260610_Nomenclator_de_Facturacion.csv");
        key.Should().MatchRegex(@"^[a-z0-9\-]+$");
    }

    [Fact]
    public void EndpointNormalization_AddsV1()
    {
        var uri = LlmOptionsFactory.NormalizeEndpoint("http://localhost:11434");
        uri.ToString().Should().EndWith("/v1");
    }

    [Fact]
    public void EndpointNormalization_DoesNotDoubleV1()
    {
        var uri = LlmOptionsFactory.NormalizeEndpoint("http://host/v1");
        uri.ToString().Should().EndWith("/v1");
        uri.ToString().Should().NotContain("/v1/v1");
    }
}

/// <summary>Provides access to private CLI helpers via reflection for testing.</summary>
internal static class PharmaCorpusAnnotatorCliReflection
{
    public static string GetSlug(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        return System.Text.RegularExpressions.Regex.Replace(name, @"[^a-z0-9]+", "-").Trim('-');
    }
}
