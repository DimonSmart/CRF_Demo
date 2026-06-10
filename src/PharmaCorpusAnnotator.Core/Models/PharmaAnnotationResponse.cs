namespace PharmaCorpusAnnotator.Core.Models;

public sealed record PharmaAnnotationResponse(
    IReadOnlyList<AnnotatedToken> Tokens,
    NormalizedPharmaItem Normalized,
    AnnotationQuality Quality);
