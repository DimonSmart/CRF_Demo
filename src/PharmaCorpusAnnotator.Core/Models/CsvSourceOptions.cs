namespace PharmaCorpusAnnotator.Core.Models;

public sealed record CsvSourceOptions(
    string InputPath,
    string SourceKey,
    string TextColumn,
    string Delimiter,
    string Encoding,
    int Skip,
    int? MaxRows,
    string? RequiredNonEmptyColumn = null);
