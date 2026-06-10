namespace PharmaCorpusAnnotator.Core.Models;

public sealed record PharmaCorpusRecord(
    long RowNumber,
    string Text,
    IReadOnlyDictionary<string, string> Context,
    PharmaAnnotationResponse Annotation);
