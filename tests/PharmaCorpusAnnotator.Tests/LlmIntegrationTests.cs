using FluentAssertions;
using Microsoft.Extensions.Logging;
using PharmaCorpusAnnotator.Cli;
using PharmaCorpusAnnotator.Core.Llm;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Tokenization;
using PharmaCorpusAnnotator.Core.Validation;

namespace PharmaCorpusAnnotator.Tests;

/// <summary>
/// Integration tests that call a real LLM endpoint.
/// Explicit by default. Run manually with xUnit.Explicit=on.
/// Requires LLM_BASE_URL, LLM_MODEL, LLM_API_KEY environment variables.
/// </summary>
public class LlmIntegrationTests
{
    [Fact(Explicit = true)]
    public async Task AnnotatesOneShortRow_WithTypedResponse()
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

        var llmOpts = LlmOptionsFactory.FromEnvironment();

        // Verify endpoint reachability
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            await http.GetAsync(
                llmOpts.BaseEndpoint.ToString().Replace("/v1", "/api/version"),
                TestContext.Current.CancellationToken);
        }
        catch
        {
            // Ollama not available — skip
            return;
        }

        var modelClient = PharmaModelClientFactory.Create(llmOpts, loggerFactory);
        var tokenizer = new PharmaTokenizer();

        var text = "captopril 4 mg/ml suspension oral 100 ml 1 frasco";
        var tokens = tokenizer.Tokenize(text);

        var request = new PharmaAnnotationModelRequest(
            Language: "es",
            SourceKey: "integration-test",
            RowNumber: 1,
            Text: text,
            Tokens: tokens,
            Context: new Dictionary<string, string>
            {
                ["Principio activo o asociación de principios activos"] = "CAPTOPRIL"
            });

        var response = await modelClient.AnnotateAsync(request, TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        response.Tokens.Should().HaveCount(tokens.Count);
        response.Tokens.Select(t => t.Text).Should().Equal(tokens.Select(t => t.Text));
        response.Normalized.Should().NotBeNull();
        response.Quality.Should().NotBeNull();

        var validator = new PharmaAnnotationValidator();
        var validation = validator.Validate(request, response);
        validation.IsValid.Should().BeTrue(
            because: $"Validation errors: {string.Join("; ", validation.Errors)}");
    }
}
