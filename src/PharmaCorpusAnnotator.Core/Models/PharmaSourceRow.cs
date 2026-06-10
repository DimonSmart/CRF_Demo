namespace PharmaCorpusAnnotator.Core.Models;

public sealed record PharmaSourceRow(
    string SourceKey,
    string FileName,
    long RowNumber,
    string TextColumn,
    string Text,
    IReadOnlyDictionary<string, string> Context);
