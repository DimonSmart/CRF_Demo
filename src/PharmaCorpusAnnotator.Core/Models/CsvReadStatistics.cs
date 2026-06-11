namespace PharmaCorpusAnnotator.Core.Models;

public sealed record CsvReadStatistics(
    long RowsRead,
    long SkippedByEmptyTextColumn,
    long SkippedByRequiredColumn,
    long AcceptedRows);
