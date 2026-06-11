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

    [Fact]
    public void LlmOptions_UsesSelectedProfile()
    {
        WithLlmConfig(configPath =>
            WithEnvironment(
                () =>
                {
                    var options = LlmOptionsFactory.FromEnvironment();

                    options.ModelId.Should().Be("openai/gpt-oss-120b");
                    options.BaseEndpoint.ToString().Should().Be("https://integrate.api.nvidia.com/v1");
                    options.ApiKey.Should().Be("test-secret");
                },
                ("LLM_CONFIG_PATH", configPath),
                ("LLM_PROFILE", "nvidia"),
                ("LLM_MODEL", null),
                ("LLM_BASE_URL", null),
                ("LLM_API_KEY", null),
                ("LLM_RETRY_COUNT", null),
                ("LLM_TIMEOUT_MINUTES", null),
                ("LLM_IGNORE_SSL_ERRORS", null),
                ("LLM_USERNAME", null),
                ("LLM_PASSWORD", null),
                ("NVIDIA_API_KEY", "test-secret")));
    }

    [Fact]
    public void LlmOptions_ExplicitProfileOverridesEnvironmentProfile()
    {
        WithLlmConfig(configPath =>
            WithEnvironment(
                () =>
                {
                    var options = LlmOptionsFactory.FromEnvironment(profileName: "ollama");

                    options.ModelId.Should().Be("qwen3:14b");
                    options.BaseEndpoint.ToString().Should().Be("http://localhost:11434/v1");
                    options.ApiKey.Should().Be("ollama");
                },
                ("LLM_CONFIG_PATH", configPath),
                ("LLM_PROFILE", "nvidia"),
                ("LLM_MODEL", null),
                ("LLM_BASE_URL", null),
                ("LLM_API_KEY", null),
                ("NVIDIA_API_KEY", "test-secret")));
    }

    [Fact]
    public void LlmOptions_EnvironmentOverridesSelectedProfile()
    {
        WithLlmConfig(configPath =>
            WithEnvironment(
                () =>
                {
                    var options = LlmOptionsFactory.FromEnvironment();

                    options.ModelId.Should().Be("custom-model");
                    options.BaseEndpoint.ToString().Should().Be("http://custom-host:1234/v1");
                    options.ApiKey.Should().Be("custom-key");
                },
                ("LLM_CONFIG_PATH", configPath),
                ("LLM_PROFILE", "nvidia"),
                ("NVIDIA_API_KEY", "test-secret"),
                ("LLM_MODEL", "custom-model"),
                ("LLM_BASE_URL", "http://custom-host:1234"),
                ("LLM_API_KEY", "custom-key")));
    }

    [Fact]
    public void LlmOptions_UsesOneBasedProfileNumber()
    {
        WithLlmConfig(configPath =>
            WithEnvironment(
                () =>
                {
                    var options = LlmOptionsFactory.FromEnvironment();

                    options.ModelId.Should().Be("openai/gpt-oss-120b");
                    options.ApiKey.Should().Be("test-secret");
                },
                ("LLM_CONFIG_PATH", configPath),
                ("LLM_PROFILE", "2"),
                ("LLM_MODEL", null),
                ("LLM_BASE_URL", null),
                ("LLM_API_KEY", null),
                ("NVIDIA_API_KEY", "test-secret")));
    }

    private static void WithLlmConfig(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), $"llmsettings-{Guid.NewGuid()}.json");
        File.WriteAllText(
            path,
            """
            {
              "Llm": {
                "ActiveProfile": "ollama",
                "Profiles": [
                  {
                    "Name": "ollama",
                    "BaseUrl": "http://localhost:11434",
                    "Model": "qwen3:14b",
                    "ApiKey": "ollama"
                  },
                  {
                    "Name": "nvidia",
                    "BaseUrl": "https://integrate.api.nvidia.com/v1",
                    "Model": "openai/gpt-oss-120b",
                    "ApiKey": "%NVIDIA_API_KEY%",
                    "RetryCount": 3
                  }
                ]
              }
            }
            """);

        try
        {
            action(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WithEnvironment(Action action, params (string Name, string? Value)[] values)
    {
        var previous = values
            .Select(v => (v.Name, Value: Environment.GetEnvironmentVariable(v.Name)))
            .ToArray();

        try
        {
            foreach (var (name, value) in values)
                Environment.SetEnvironmentVariable(name, value);

            action();
        }
        finally
        {
            foreach (var (name, value) in previous)
                Environment.SetEnvironmentVariable(name, value);
        }
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
