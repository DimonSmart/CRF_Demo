using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using PharmaCorpusAnnotator.Core.Labeling;
using PharmaCorpusAnnotator.Core.Llm;
using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Tests;

public class PharmaAnnotationPromptBuilderTests
{
    private readonly PharmaAnnotationPromptBuilder _sut = new();

    [Fact]
    public void BuildUserPrompt_SerializesOnlyTokensAndTokenCount()
    {
        var request = MakeRequest("ibuprofeno 600 mg");

        using var document = JsonDocument.Parse(_sut.BuildUserPrompt(request));
        var root = document.RootElement;

        root.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["tokens", "tokenCount"]);
        root.GetProperty("tokens").EnumerateArray().Select(e => e.GetString()).Should()
            .Equal("ibuprofeno", "600", "mg");
        root.GetProperty("tokenCount").GetInt32().Should().Be(3);

        root.TryGetProperty("allowedLabels", out _).Should().BeFalse();
        root.TryGetProperty("sourceKey", out _).Should().BeFalse();
        root.TryGetProperty("rowNumber", out _).Should().BeFalse();
        root.TryGetProperty("language", out _).Should().BeFalse();
        root.TryGetProperty("context", out _).Should().BeFalse();
        root.TryGetProperty("text", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildSystemPrompt_ContainsEveryCanonicalLabel()
    {
        var prompt = _sut.BuildSystemPrompt();

        foreach (var label in PharmaAnnotationLabels.All)
            prompt.Should().Contain(label);
    }

    [Fact]
    public void PharmaAnnotationModelRequest_DoesNotExposeAllowedLabels()
    {
        typeof(PharmaAnnotationModelRequest)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(p => p.Name)
            .Should()
            .NotContain("AllowedLabels");
    }

    [Fact]
    public void PharmaLabelAnnotationResponse_UsesStringArrayLabels()
    {
        typeof(PharmaLabelAnnotationResponse)
            .GetProperty(nameof(PharmaLabelAnnotationResponse.Labels))!
            .PropertyType
            .Should()
            .Be(typeof(string[]));
    }

    private static PharmaAnnotationModelRequest MakeRequest(string text)
    {
        var parts = text.Split(' ');
        var tokens = parts
            .Select((part, index) => new SourceToken(index, part, index, index + part.Length))
            .ToList();

        return new PharmaAnnotationModelRequest(
            "es",
            "source",
            10,
            text,
            tokens,
            new Dictionary<string, string> { ["ctx"] = "value" });
    }
}
