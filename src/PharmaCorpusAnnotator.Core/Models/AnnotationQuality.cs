namespace PharmaCorpusAnnotator.Core.Models;

public sealed record AnnotationQuality(
    double? Confidence,
    IReadOnlyList<string> Warnings);
