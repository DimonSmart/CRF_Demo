namespace PharmaCorpusAnnotator.Core.Models;

public static class LabelSchema
{
    public static readonly IReadOnlyList<string> SimpleEntityTypes =
    [
        "ACTIVE_INGREDIENT",
        "STRENGTH",
        "DOSE_FORM",
        "ROUTE",
        "PACKAGE_QUANTITY",
        "PACKAGE_UNIT",
        "PACKAGE_VOLUME",
        "REGULATORY_MARKER",
    ];

    public static readonly IReadOnlyList<string> AllLabels = BuildBioLabels(SimpleEntityTypes);

    public static readonly IReadOnlyList<string> FullLabels =
    [
        "O",
        "B-PRODUCT_NAME", "I-PRODUCT_NAME",
        "B-BRAND", "I-BRAND",
        "B-GENERIC_NAME", "I-GENERIC_NAME",
        "B-MANUFACTURER", "I-MANUFACTURER",
        "B-ACTIVE_INGREDIENT", "I-ACTIVE_INGREDIENT",
        "B-STRENGTH", "I-STRENGTH",
        "B-DOSE_FORM", "I-DOSE_FORM",
        "B-ROUTE", "I-ROUTE",
        "B-PACKAGE_QUANTITY", "I-PACKAGE_QUANTITY",
        "B-PACKAGE_UNIT", "I-PACKAGE_UNIT",
        "B-PACKAGE_VOLUME", "I-PACKAGE_VOLUME",
        "B-PRICE", "I-PRICE",
        "B-REGULATORY_MARKER", "I-REGULATORY_MARKER",
    ];

    public static IReadOnlyList<string> GetLabels(string schema) =>
        schema.Equals("full", StringComparison.OrdinalIgnoreCase)
            ? FullLabels
            : AllLabels;

    private static IReadOnlyList<string> BuildBioLabels(IReadOnlyList<string> entityTypes)
    {
        var labels = new List<string> { "O" };
        foreach (var entityType in entityTypes)
        {
            labels.Add($"B-{entityType}");
            labels.Add($"I-{entityType}");
        }

        return labels;
    }
}
