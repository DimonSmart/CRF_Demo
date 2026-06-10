namespace PharmaCorpusAnnotator.Core.Models;

public sealed record PharmaCorpusSourceHeader(
    string SourceKey,
    string FileName,
    string Format,
    string Encoding,
    string Delimiter,
    string TextColumn,
    IReadOnlyList<string> ContextColumns);
