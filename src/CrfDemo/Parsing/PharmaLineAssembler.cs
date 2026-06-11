using CrfDemo.Inference;

namespace CrfDemo.Parsing;

public sealed class PharmaLineAssembler
{
    private readonly BioEntityExtractor _extractor = new();

    public ParsedPharmaLine Assemble(string originalText, IReadOnlyList<TaggedToken> tokens)
    {
        var extraction = _extractor.Extract(tokens);
        var grouped = extraction.Entities
            .GroupBy(e => e.Type, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Text).ToArray(), StringComparer.Ordinal);

        var warnings = extraction.Warnings.ToList();
        var packageQuantityText = First(grouped, "PACKAGE_QUANTITY");
        int? packageQuantity = null;
        if (!string.IsNullOrWhiteSpace(packageQuantityText))
        {
            var digits = new string(packageQuantityText.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var value))
                packageQuantity = value;
            else
                warnings.Add($"Package quantity is not numeric: {packageQuantityText}.");
        }

        return new ParsedPharmaLine
        {
            OriginalText = originalText,
            Tokens = tokens,
            ActiveIngredients = All(grouped, "ACTIVE_INGREDIENT"),
            Strengths = All(grouped, "STRENGTH"),
            DoseForm = First(grouped, "DOSE_FORM"),
            Route = First(grouped, "ROUTE"),
            PackageVolume = First(grouped, "PACKAGE_VOLUME"),
            PackageQuantity = packageQuantity,
            PackageQuantityText = packageQuantityText,
            PackageUnit = First(grouped, "PACKAGE_UNIT"),
            RegulatoryMarkers = All(grouped, "REGULATORY_MARKER"),
            OtherTokens = tokens.Where(t => t.Label == "O").Select(t => t.Token).ToArray(),
            Warnings = warnings
        };
    }

    private static IReadOnlyList<string> All(Dictionary<string, string[]> grouped, string key)
        => grouped.TryGetValue(key, out var values) ? values : Array.Empty<string>();

    private static string? First(Dictionary<string, string[]> grouped, string key)
        => grouped.TryGetValue(key, out var values) ? values.FirstOrDefault() : null;
}
