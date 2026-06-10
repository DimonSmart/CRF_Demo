using PharmaCorpusAnnotator.Core.Labeling;
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
        var response = new PharmaLabelAnnotationResponse
        {
            Labels =
            [
                PharmaAnnotationLabels.ActiveIngredientBegin,
                PharmaAnnotationLabels.StrengthBegin,
                PharmaAnnotationLabels.StrengthInside,
            ],
        };

        var result = _sut.Validate(request, response);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void LabelCountMismatch_IsInvalid()
    {
        var request = MakeRequest("captopril 4 mg/ml");
        var response = new PharmaLabelAnnotationResponse
        {
            Labels = [PharmaAnnotationLabels.ActiveIngredientBegin, PharmaAnnotationLabels.StrengthBegin],
        };

        var result = _sut.Validate(request, response);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("labels count mismatch"));
    }

    [Fact]
    public void UnknownLabel_IsInvalid()
    {
        var request = MakeRequest("captopril");
        var response = new PharmaLabelAnnotationResponse { Labels = ["B-DOSAGE"] };

        var result = _sut.Validate(request, response);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not allowed") && e.Contains("B-DOSAGE"));
    }

    [Fact]
    public void InvalidBioTransition_IsInvalid()
    {
        var request = MakeRequest("captopril 4");
        var response = new PharmaLabelAnnotationResponse
        {
            Labels =
            [
                PharmaAnnotationLabels.ActiveIngredientBegin,
                PharmaAnnotationLabels.StrengthInside,
            ],
        };

        var result = _sut.Validate(request, response);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("invalid BIO transition"));
    }

    [Fact]
    public void InsideLabelAfterOutside_IsInvalid()
    {
        var request = MakeRequest("marca 4");
        var response = new PharmaLabelAnnotationResponse
        {
            Labels =
            [
                PharmaAnnotationLabels.Outside,
                PharmaAnnotationLabels.StrengthInside,
            ],
        };

        var result = _sut.Validate(request, response);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("I-STRENGTH after O"));
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
            tokens);
    }
}
