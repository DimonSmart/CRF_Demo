using FluentAssertions;
using PharmaCorpusAnnotator.Core.Labeling;
using PharmaCorpusAnnotator.Core.Mapping;
using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Tests;

public class LabelAnnotationMapperTests
{
    private readonly LabelAnnotationMapper _sut = new();

    [Fact]
    public void Map_CreatesAnnotatedTokensFromRequestTokensAndLabels()
    {
        var request = MakeRequest("captopril 4 mg/ml suspension oral 100 ml 1 frasco");
        var labelResponse = new PharmaLabelAnnotationResponse
        {
            Labels =
            [
                PharmaAnnotationLabels.ActiveIngredientBegin,
                PharmaAnnotationLabels.StrengthBegin,
                PharmaAnnotationLabels.StrengthInside,
                PharmaAnnotationLabels.DoseFormBegin,
                PharmaAnnotationLabels.RouteBegin,
                PharmaAnnotationLabels.PackageVolumeBegin,
                PharmaAnnotationLabels.PackageVolumeInside,
                PharmaAnnotationLabels.PackageQuantityBegin,
                PharmaAnnotationLabels.PackageUnitBegin,
            ],
        };

        var response = _sut.Map(request, labelResponse);

        response.Tokens.Select(t => t.Index).Should().Equal(request.Tokens.Select(t => t.Index));
        response.Tokens.Select(t => t.Text).Should().Equal(request.Tokens.Select(t => t.Text));
        response.Tokens.Select(t => t.Label).Should().Equal(labelResponse.Labels);
        response.Tokens.Should().OnlyContain(t => t.Confidence == null);
        response.Normalized.ActiveIngredients.Should().Equal("captopril");
        response.Normalized.Strength.Should().Be("4 mg/ml");
        response.Normalized.DoseForm.Should().Be("suspension");
        response.Normalized.Route.Should().Be("oral");
        response.Normalized.PackageVolume.Should().Be("100 ml");
        response.Normalized.PackageQuantity.Should().Be(1);
        response.Normalized.PackageUnit.Should().Be("frasco");
        response.Quality.Confidence.Should().BeNull();
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
            new Dictionary<string, string>());
    }
}
