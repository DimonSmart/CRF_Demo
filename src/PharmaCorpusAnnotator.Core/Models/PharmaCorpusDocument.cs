namespace PharmaCorpusAnnotator.Core.Models;

public sealed record PharmaCorpusDocument(
    string SchemaVersion,
    DateTimeOffset CreatedAt,
    AnnotationSchema AnnotationSchema,
    IReadOnlyList<PharmaCorpusSourceBlock> Sources);
