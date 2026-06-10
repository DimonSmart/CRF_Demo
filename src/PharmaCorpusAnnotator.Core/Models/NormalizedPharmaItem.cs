namespace PharmaCorpusAnnotator.Core.Models;

public sealed record NormalizedPharmaItem(
    string? ProductName,
    string? Brand,
    string? Manufacturer,
    IReadOnlyList<string> ActiveIngredients,
    string? Strength,
    string? DoseForm,
    string? Route,
    int? PackageQuantity,
    string? PackageUnit,
    string? PackageVolume,
    decimal? Price,
    string? Currency);
