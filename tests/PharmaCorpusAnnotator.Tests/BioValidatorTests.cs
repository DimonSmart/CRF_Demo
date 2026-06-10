using FluentAssertions;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Validation;

namespace PharmaCorpusAnnotator.Tests;

public class BioValidatorTests
{
    private static readonly IReadOnlyList<string> AllowedLabels = LabelSchema.AllLabels;
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
            Context: new Dictionary<string, string>(),
            AllowedLabels: AllowedLabels);

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
        var res = MakeResponse("B-ST", "I-ST");
        _sut.Validate(req, res).IsValid.Should().BeTrue();
    }

    [Fact]
    public void IStrength_AsFirstToken_IsInvalid()
    {
        var req = MakeRequest(2);
        var res = MakeResponse("I-ST", "B-ST");
        var result = _sut.Validate(req, res);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("I-ST") && e.Contains("first token"));
    }

    [Fact]
    public void BActiveIngredient_IStrength_IsInvalid()
    {
        var req = MakeRequest(2);
        var res = MakeResponse("B-AI", "I-ST");
        var result = _sut.Validate(req, res);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("I-ST") && e.Contains("B-AI"));
    }

    [Fact]
    public void BDoseForm_IDoseForm_IsValid()
    {
        var req = MakeRequest(2);
        var res = MakeResponse("B-DF", "I-DF");
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
        var res = MakeResponse("B-ST", "I-DF");
        var result = _sut.Validate(req, res);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("I-DF") && e.Contains("B-ST"));
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
        var res = MakeResponse("B-DF", "I-DF", "I-DF");
        _sut.Validate(req, res).IsValid.Should().BeTrue();
    }
}
