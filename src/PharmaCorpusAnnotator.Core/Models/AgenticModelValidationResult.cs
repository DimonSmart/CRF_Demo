namespace PharmaCorpusAnnotator.Core.Models;

public sealed record AgenticModelValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static AgenticModelValidationResult Success() =>
        new(true, Array.Empty<string>());

    public static AgenticModelValidationResult Failure(params string[] errors) =>
        new(false, errors);
}
