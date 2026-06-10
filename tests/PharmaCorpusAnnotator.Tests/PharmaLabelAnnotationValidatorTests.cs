using FluentAssertions;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Validation;

namespace PharmaCorpusAnnotator.Tests;

public class PharmaLabelAnnotationValidatorTests
{
    private readonly PharmaLabelAnnotationValidator _sut = new();

    [Fact]
    public void ValidLabelArray_Passes()
    {
        var request = MakeRequest("captopril 4 mg/ml");
        var response = new PharmaLabelAnnotationResponse(["B-AI", "B-ST", "I-ST"]);

        var result = _sut.Validate(request, response);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void LabelCountMismatch_IsInvalid()
    {
        var request = MakeRequest("captopril 4 mg/ml");
        var response = new PharmaLabelAnnotationResponse(["B-AI", "B-ST"]);

        var result = _sut.Validate(request, response);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Label count mismatch"));
    }

    [Fact]
    public void UnknownLabel_IsInvalid()
    {
        var request = MakeRequest("captopril");
        var response = new PharmaLabelAnnotationResponse(["B-DOSAGE"]);

        var result = _sut.Validate(request, response);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Unknown label") && e.Contains("B-DOSAGE"));
    }

    [Fact]
    public void InvalidBioTransition_IsInvalid()
    {
        var request = MakeRequest("captopril 4");
        var response = new PharmaLabelAnnotationResponse(["B-AI", "I-ST"]);

        var result = _sut.Validate(request, response);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid BIO transition"));
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
