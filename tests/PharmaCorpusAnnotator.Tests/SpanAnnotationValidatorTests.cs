using FluentAssertions;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Validation;

namespace PharmaCorpusAnnotator.Tests;

public class SpanAnnotationValidatorTests
{
    private readonly PharmaSpanAnnotationValidator _sut = new();

    [Fact]
    public void ValidSpanResponse_Passes()
    {
        var request = MakeRequest("captopril 4 mg/ml");
        var response = new PharmaSpanAnnotationResponse(
            [
                new PharmaEntitySpan("ACTIVE_INGREDIENT", 0, 0, 0.9),
                new PharmaEntitySpan("STRENGTH", 1, 2, 0.9),
            ],
            false,
            []);

        var result = _sut.Validate(request, response);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SpanOutsideTokenRange_IsInvalid()
    {
        var request = MakeRequest("captopril");
        var response = new PharmaSpanAnnotationResponse(
            [new PharmaEntitySpan("ACTIVE_INGREDIENT", 0, 2, null)],
            false,
            []);

        var result = _sut.Validate(request, response);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("outside token range"));
    }

    [Fact]
    public void OverlappingSpans_AreInvalid()
    {
        var request = MakeRequest("captopril 4 mg/ml");
        var response = new PharmaSpanAnnotationResponse(
            [
                new PharmaEntitySpan("ACTIVE_INGREDIENT", 0, 1, null),
                new PharmaEntitySpan("STRENGTH", 1, 2, null),
            ],
            false,
            []);

        var result = _sut.Validate(request, response);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("overlap"));
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
