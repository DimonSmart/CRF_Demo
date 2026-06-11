using CrfDemo.Features;
using FluentAssertions;

namespace PharmaCorpusAnnotator.Tests;

public class TokenFeatureExtractorTests
{
    private readonly TokenFeatureExtractor _sut = new();

    [Theory]
    [InlineData("22", "isInteger=True")]
    [InlineData("100", "isInteger=True")]
    [InlineData("0", "isInteger=True")]
    [InlineData("аб", "isInteger=False")]
    [InlineData("2,95", "isInteger=False")]
    [InlineData("3.10", "isInteger=False")]
    [InlineData("20MG", "isInteger=False")]
    [InlineData("mg/ml", "isInteger=False")]
    [InlineData(",", "isInteger=False")]
    [InlineData(".", "isInteger=False")]
    public void Extract_AddsIntegerFeature(string token, string expectedFeature)
    {
        var tokenFeatures = _sut.Extract([token]);

        tokenFeatures[0].Values.Should().Contain(expectedFeature);
    }
}
