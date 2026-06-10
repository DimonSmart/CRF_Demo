namespace PharmaCorpusAnnotator.Core.Models;

public sealed record PharmaAnnotationModelRequest(
    string Language,
    string SourceKey,
    long RowNumber,
    string Text,
    IReadOnlyList<SourceToken> Tokens,
    IReadOnlyDictionary<string, string> Context);
