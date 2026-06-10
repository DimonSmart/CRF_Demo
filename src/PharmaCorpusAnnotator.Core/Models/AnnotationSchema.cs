namespace PharmaCorpusAnnotator.Core.Models;

public sealed record AnnotationSchema(
    string Language,
    IReadOnlyList<string> Labels);
