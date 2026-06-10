namespace PharmaCorpusAnnotator.Core.Labeling;

public static class PharmaAnnotationLabels
{
    public const string Outside = "O";

    public const string ActiveIngredient = "ACTIVE_INGREDIENT";
    public const string Strength = "STRENGTH";
    public const string DoseForm = "DOSE_FORM";
    public const string Route = "ROUTE";
    public const string PackageVolume = "PACKAGE_VOLUME";
    public const string PackageQuantity = "PACKAGE_QUANTITY";
    public const string PackageUnit = "PACKAGE_UNIT";
    public const string RegulatoryMarker = "REGULATORY_MARKER";

    public const string ActiveIngredientBegin = "B-ACTIVE_INGREDIENT";
    public const string ActiveIngredientInside = "I-ACTIVE_INGREDIENT";

    public const string StrengthBegin = "B-STRENGTH";
    public const string StrengthInside = "I-STRENGTH";

    public const string DoseFormBegin = "B-DOSE_FORM";
    public const string DoseFormInside = "I-DOSE_FORM";

    public const string RouteBegin = "B-ROUTE";
    public const string RouteInside = "I-ROUTE";

    public const string PackageVolumeBegin = "B-PACKAGE_VOLUME";
    public const string PackageVolumeInside = "I-PACKAGE_VOLUME";

    public const string PackageQuantityBegin = "B-PACKAGE_QUANTITY";
    public const string PackageQuantityInside = "I-PACKAGE_QUANTITY";

    public const string PackageUnitBegin = "B-PACKAGE_UNIT";
    public const string PackageUnitInside = "I-PACKAGE_UNIT";

    public const string RegulatoryMarkerBegin = "B-REGULATORY_MARKER";
    public const string RegulatoryMarkerInside = "I-REGULATORY_MARKER";

    public static readonly string[] All =
    [
        Outside,
        ActiveIngredientBegin,
        ActiveIngredientInside,
        StrengthBegin,
        StrengthInside,
        DoseFormBegin,
        DoseFormInside,
        RouteBegin,
        RouteInside,
        PackageVolumeBegin,
        PackageVolumeInside,
        PackageQuantityBegin,
        PackageQuantityInside,
        PackageUnitBegin,
        PackageUnitInside,
        RegulatoryMarkerBegin,
        RegulatoryMarkerInside,
    ];

    public static readonly IReadOnlySet<string> AllSet =
        new HashSet<string>(All, StringComparer.Ordinal);
}
