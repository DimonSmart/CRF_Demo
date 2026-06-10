namespace PharmaCorpusAnnotator.Core.Models;

public sealed record PharmaSpanAnnotationResponse(
    IReadOnlyList<PharmaEntitySpan> Spans,
    bool NeedsReview,
    IReadOnlyList<string> Warnings);
