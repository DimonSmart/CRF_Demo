namespace PharmaCorpusAnnotator.Core.Models;

public sealed record FailedRecord(
    string SourceKey,
    long RowNumber,
    string Text,
    IReadOnlyDictionary<string, string> Context,
    FailedRecordError Error);

public sealed record FailedRecordError(
    string Kind,
    string Message,
    int Attempts);
