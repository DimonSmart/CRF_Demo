using FluentAssertions;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Validation;

namespace PharmaCorpusAnnotator.Tests;

public class ResponseValidatorTests
{
    private readonly PharmaAnnotationValidator _sut = new();

    private static PharmaAnnotationModelRequest MakeRequest(int tokenCount = 3,
        Dictionary<string, string>? context = null) =>
        new(
            Language: "es",
            SourceKey: "test",
            RowNumber: 1,
            Text: "tok0 tok1 tok2",
            Tokens: Enumerable.Range(0, tokenCount)
                .Select(i => new SourceToken(i, $"tok{i}", i * 5, i * 5 + 4))
                .ToList(),
            Context: context ?? new Dictionary<string, string>(),
            AllowedLabels: LabelSchema.AllLabels);

    private static PharmaAnnotationResponse MakeResponse(
        int tokenCount = 3,
        string[]? labels = null,
        decimal? price = null,
        List<string>? warnings = null)
    {
        var effectiveLabels = labels ?? Enumerable.Repeat("O", tokenCount).ToArray();
        var tokens = effectiveLabels.Select((l, i) =>
            new AnnotatedToken(i, $"tok{i}", l, null, null)).ToList();

        return new PharmaAnnotationResponse(
            tokens,
            new NormalizedPharmaItem(null, null, null, [], null, null, null, null, null, null, price, null),
            new AnnotationQuality(null, false, warnings ?? []));
    }

    [Fact]
    public void MissingToken_IsInvalid()
    {
        var req = MakeRequest(3);
        var res = MakeResponse(2);
        var result = _sut.Validate(req, res);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("2 tokens") && e.Contains("3 tokens"));
    }

    [Fact]
    public void ExtraToken_IsInvalid()
    {
        var req = MakeRequest(3);
        var res = MakeResponse(4);
        _sut.Validate(req, res).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ChangedTokenText_IsInvalid()
    {
        var req = MakeRequest(1);
        var tokens = new List<AnnotatedToken>
        {
            new(0, "WRONG_TEXT", "O", null, null)
        };
        var res = new PharmaAnnotationResponse(
            tokens,
            new NormalizedPharmaItem(null, null, null, [], null, null, null, null, null, null, null, null),
            new AnnotationQuality(null, false, []));

        var result = _sut.Validate(req, res);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("WRONG_TEXT"));
    }

    [Fact]
    public void ChangedTokenIndex_IsInvalid()
    {
        var req = MakeRequest(1);
        var tokens = new List<AnnotatedToken>
        {
            new(99, "tok0", "O", null, null)
        };
        var res = new PharmaAnnotationResponse(
            tokens,
            new NormalizedPharmaItem(null, null, null, [], null, null, null, null, null, null, null, null),
            new AnnotationQuality(null, false, []));

        var result = _sut.Validate(req, res);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("index"));
    }

    [Fact]
    public void ConfidenceBelowZero_IsInvalid()
    {
        var req = MakeRequest(1);
        var tokens = new List<AnnotatedToken>
        {
            new(0, "tok0", "O", null, -0.1)
        };
        var res = new PharmaAnnotationResponse(
            tokens,
            new NormalizedPharmaItem(null, null, null, [], null, null, null, null, null, null, null, null),
            new AnnotationQuality(null, false, []));

        _sut.Validate(req, res).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ConfidenceAboveOne_IsInvalid()
    {
        var req = MakeRequest(1);
        var tokens = new List<AnnotatedToken>
        {
            new(0, "tok0", "O", null, 1.5)
        };
        var res = new PharmaAnnotationResponse(
            tokens,
            new NormalizedPharmaItem(null, null, null, [], null, null, null, null, null, null, null, null),
            new AnnotationQuality(null, false, []));

        _sut.Validate(req, res).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidResponse_PassesAllChecks()
    {
        var req = MakeRequest(3);
        var res = MakeResponse(3, ["B-AI", "B-ST", "O"]);
        _sut.Validate(req, res).IsValid.Should().BeTrue();
    }
}
