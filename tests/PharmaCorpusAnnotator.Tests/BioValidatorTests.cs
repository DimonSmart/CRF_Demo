using FluentAssertions;
using PharmaCorpusAnnotator.Core.Labeling;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Validation;

namespace PharmaCorpusAnnotator.Tests;

public class BioValidatorTests
{
    private readonly PharmaAnnotationValidator _sut = new();

    private static PharmaAnnotationModelRequest MakeRequest(int tokenCount) =>
        new(
            Language: "es",
            SourceKey: "test",
            RowNumber: 1,
            Text: string.Join(" ", Enumerable.Range(0, tokenCount).Select(i => $"tok{i}")),
            Tokens: Enumerable.Range(0, tokenCount)
                .Select(i => new SourceToken(i, $"tok{i}", i * 5, i * 5 + 4))
                .ToList(),
            Context: new Dictionary<string, string>());

    private static PharmaAnnotationResponse MakeResponse(params string[] labels)
    {
        var tokens = labels.Select((l, i) =>
            new AnnotatedToken(i, $"tok{i}", l, null, null)).ToList();
        return new PharmaAnnotationResponse(
            tokens,
            new NormalizedPharmaItem(null, null, null, [], null, null, null, null, null, null, null, null),
            new AnnotationQuality(null, false, []));
    }

    [Fact]
    public void BStrength_IStrength_IsValid()
    {
        var req = MakeRequest(2);
        var res = MakeResponse(PharmaAnnotationLabels.StrengthBegin, PharmaAnnotationLabels.StrengthInside);
        _sut.Validate(req, res).IsValid.Should().BeTrue();
    }

    [Fact]
    public void IStrength_AsFirstToken_IsInvalid()
    {
        var req = MakeRequest(2);
        var res = MakeResponse(PharmaAnnotationLabels.StrengthInside, PharmaAnnotationLabels.StrengthBegin);
        var result = _sut.Validate(req, res);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("I-STRENGTH") && e.Contains("first token"));
    }

    [Fact]
    public void BActiveIngredient_IStrength_IsInvalid()
    {
        var req = MakeRequest(2);
        var res = MakeResponse(PharmaAnnotationLabels.ActiveIngredientBegin, PharmaAnnotationLabels.StrengthInside);
        var result = _sut.Validate(req, res);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("I-STRENGTH") && e.Contains("B-ACTIVE_INGREDIENT"));
    }

    [Fact]
    public void BDoseForm_IDoseForm_IsValid()
    {
        var req = MakeRequest(2);
        var res = MakeResponse(PharmaAnnotationLabels.DoseFormBegin, PharmaAnnotationLabels.DoseFormInside);
        _sut.Validate(req, res).IsValid.Should().BeTrue();
    }

    [Fact]
    public void UnsupportedLabel_IsInvalid()
    {
        var req = MakeRequest(1);
        var res = MakeResponse("B-UNKNOWN");
        var result = _sut.Validate(req, res);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("B-UNKNOWN"));
    }

    [Fact]
    public void BStrength_IDoseForm_IsInvalid_CrossEntity()
    {
        var req = MakeRequest(2);
        var res = MakeResponse(PharmaAnnotationLabels.StrengthBegin, PharmaAnnotationLabels.DoseFormInside);
        var result = _sut.Validate(req, res);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("I-DOSE_FORM") && e.Contains("B-STRENGTH"));
    }

    [Fact]
    public void AllO_IsValid()
    {
        var req = MakeRequest(3);
        var res = MakeResponse("O", "O", "O");
        _sut.Validate(req, res).IsValid.Should().BeTrue();
    }

    [Fact]
    public void IDoseForm_After_IDoseForm_IsValid()
    {
        var req = MakeRequest(3);
        var res = MakeResponse(
            PharmaAnnotationLabels.DoseFormBegin,
            PharmaAnnotationLabels.DoseFormInside,
            PharmaAnnotationLabels.DoseFormInside);
        _sut.Validate(req, res).IsValid.Should().BeTrue();
    }
}
