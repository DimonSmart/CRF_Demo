namespace PharmaCorpusAnnotator.Core.Models;

public static class LabelSchema
{
    public const string ActiveIngredient = "AI";
    public const string Strength = "ST";
    public const string DoseForm = "DF";
    public const string Route = "RO";
    public const string PackageVolume = "PV";
    public const string PackageQuantity = "PQ";
    public const string PackageUnit = "PU";
    public const string RegulatoryMarker = "RM";

    public static readonly IReadOnlyList<string> EntityTypes =
    [
        ActiveIngredient,
        Strength,
        DoseForm,
        Route,
        PackageVolume,
        PackageQuantity,
        PackageUnit,
        RegulatoryMarker,
    ];

    public static readonly IReadOnlyList<string> AllLabels = BuildBioLabels(EntityTypes);

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
