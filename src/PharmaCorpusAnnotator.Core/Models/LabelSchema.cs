namespace PharmaCorpusAnnotator.Core.Models;

public static class LabelSchema
{
    public static readonly IReadOnlyList<string> AllLabels =
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
}
