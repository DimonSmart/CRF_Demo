using FluentAssertions;
using PharmaCorpusAnnotator.Core.Mapping;
using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Tests;

public class SpanAnnotationMapperTests
{
    private readonly SpanAnnotationMapper _sut = new();

    [Fact]
    public void Map_BuildsBioForOneMultiTokenSpan()
    {
        var request = MakeRequest("4 mg/ml suspension");
        var spanResponse = new PharmaSpanAnnotationResponse(
            [new PharmaEntitySpan("STRENGTH", 0, 1, 0.8)],
            false,
            []);

        var response = _sut.Map(request, spanResponse);

        response.Tokens.Select(t => t.Label)
            .Should().Equal("B-STRENGTH", "I-STRENGTH", "O");
        response.Normalized.Strength.Should().Be("4 mg/ml");
    }

    [Fact]
    public void Map_BuildsBioForMultipleSpans()
    {
        var request = MakeRequest("captopril 4 mg/ml suspension oral");
        var spanResponse = new PharmaSpanAnnotationResponse(
            [
                new PharmaEntitySpan("ACTIVE_INGREDIENT", 0, 0, 0.9),
                new PharmaEntitySpan("STRENGTH", 1, 2, 0.8),
                new PharmaEntitySpan("DOSE_FORM", 3, 3, 0.7),
                new PharmaEntitySpan("ROUTE", 4, 4, 0.6),
            ],
            false,
            []);

        var response = _sut.Map(request, spanResponse);

        response.Tokens.Select(t => t.Label)
            .Should().Equal(
                "B-ACTIVE_INGREDIENT",
                "B-STRENGTH",
                "I-STRENGTH",
                "B-DOSE_FORM",
                "B-ROUTE");
        response.Normalized.ActiveIngredients.Should().Equal("captopril");
        response.Normalized.DoseForm.Should().Be("suspension");
        response.Normalized.Route.Should().Be("oral");
        response.Quality.Confidence.Should().BeApproximately(0.75, 0.0001);
    }

    private static PharmaAnnotationModelRequest MakeRequest(string text)
    {
        var parts = text.Split(' ');
        var tokens = parts
            .Select((part, index) => new SourceToken(index, part, index, index + part.Length))
            .ToList();

        return new PharmaAnnotationModelRequest(
            "es",
            "test",
            1,
            text,
            tokens,
            new Dictionary<string, string>(),
            LabelSchema.AllLabels);
    }
}
