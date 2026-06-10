namespace PharmaCorpusAnnotator.Core.Models;

public sealed record CsvSourceOptions(
    string InputPath,
    string SourceKey,
    string TextColumn,
    string Delimiter,
    string Encoding,
    IReadOnlyList<string> ContextColumns,
    int Skip,
    int? MaxRows);
