namespace PharmaCorpusAnnotator.Core.Models;

public sealed record AnnotationQuality(
    double? Confidence,
    bool NeedsReview,
    IReadOnlyList<string> Warnings);
