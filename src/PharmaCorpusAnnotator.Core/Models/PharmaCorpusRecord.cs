namespace PharmaCorpusAnnotator.Core.Models;

public sealed record PharmaCorpusRecord(
    long RowNumber,
    string Text,
    PharmaAnnotationResponse Annotation);
