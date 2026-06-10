namespace PharmaCorpusAnnotator.Core.Models;

public sealed record AnnotatedToken(
    int Index,
    string Text,
    string Label,
    string? Normalized,
    double? Confidence);
