using CrfDemo.Inference;

namespace CrfDemo.Parsing;

public sealed class ParsedPharmaLine
{
    public string OriginalText { get; init; } = "";
    public IReadOnlyList<TaggedToken> Tokens { get; init; } = Array.Empty<TaggedToken>();
    public IReadOnlyList<string> ActiveIngredients { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Strengths { get; init; } = Array.Empty<string>();
    public string? DoseForm { get; init; }
    public string? Route { get; init; }
    public string? PackageVolume { get; init; }
    public int? PackageQuantity { get; init; }
    public string? PackageQuantityText { get; init; }
    public string? PackageUnit { get; init; }
    public IReadOnlyList<string> RegulatoryMarkers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> OtherTokens { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
