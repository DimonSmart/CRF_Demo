namespace PharmaCorpusAnnotator.Core.Models;

public sealed record PharmaLabelAnnotationResponse(
    IReadOnlyList<string> Labels);
