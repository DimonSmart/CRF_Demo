using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Csv;

public sealed class CsvPharmaSourceReader
{
    private readonly ILogger<CsvPharmaSourceReader> _logger;

    public CsvPharmaSourceReader(ILogger<CsvPharmaSourceReader> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<PharmaSourceRow> ReadAsync(
        CsvSourceOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var encoding = ResolveEncoding(options.Encoding);
        var fileName = Path.GetFileName(options.InputPath);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = options.Delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.None,
        };

        using var reader = new StreamReader(options.InputPath, encoding, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();

        if (!headers.Contains(options.TextColumn))
            throw new InvalidOperationException(
                $"Text column '{options.TextColumn}' not found in CSV. Available: {string.Join(", ", headers)}");

        if (!string.IsNullOrWhiteSpace(options.RequiredNonEmptyColumn) &&
            !headers.Contains(options.RequiredNonEmptyColumn))
        {
            throw new InvalidOperationException(
                $"Required non-empty column not found: {options.RequiredNonEmptyColumn}");
        }

        long physicalRow = 1; // header was row 1
        long skippedBySkipOption = 0;
        long rowsRead = 0;
        long skippedByEmptyTextColumn = 0;
        long skippedByRequiredColumn = 0;
        long acceptedRows = 0;

        try
        {
            while (!options.MaxRows.HasValue || acceptedRows < options.MaxRows.Value)
            {
                if (!await csv.ReadAsync())
                    break;

                cancellationToken.ThrowIfCancellationRequested();
                physicalRow++;
                rowsRead++;

                string text;
                try
                {
                    text = csv.GetField(options.TextColumn) ?? "";
                }
                catch (Exception ex)
                {
                    skippedByEmptyTextColumn++;
                    _logger.LogWarning(ex, "Could not read text column at row {Row}, skipping.", physicalRow);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    skippedByEmptyTextColumn++;
                    _logger.LogWarning("Empty text at row {Row}, skipping.", physicalRow);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(options.RequiredNonEmptyColumn))
                {
                    var requiredValue = csv.GetField(options.RequiredNonEmptyColumn) ?? "";
                    if (string.IsNullOrWhiteSpace(requiredValue))
                    {
                        skippedByRequiredColumn++;
                        continue;
                    }
                }

                if (options.Skip > 0 && skippedBySkipOption < options.Skip)
                {
                    skippedBySkipOption++;
                    continue;
                }

                acceptedRows++;
                yield return new PharmaSourceRow(
                    options.SourceKey,
                    fileName,
                    physicalRow,
                    options.TextColumn,
                    text.Trim());
            }
        }
        finally
        {
            var stats = new CsvReadStatistics(
                rowsRead,
                skippedByEmptyTextColumn,
                skippedByRequiredColumn,
                acceptedRows);

            _logger.LogInformation("CSV rows read:                         {RowsRead}", stats.RowsRead);
            _logger.LogInformation(
                "CSV rows skipped by empty text column:  {Skipped}",
                stats.SkippedByEmptyTextColumn);

            if (!string.IsNullOrWhiteSpace(options.RequiredNonEmptyColumn))
            {
                _logger.LogInformation(
                    "CSV rows skipped by required column:    {Skipped}",
                    stats.SkippedByRequiredColumn);
            }

            _logger.LogInformation("CSV rows accepted:                     {Accepted}", stats.AcceptedRows);
        }
    }

    private static Encoding ResolveEncoding(string name) =>
        name.ToLowerInvariant() switch
        {
            "utf-8-sig" or "utf8-sig" or "utf-8" or "utf8" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            "latin-1" or "iso-8859-1" => Encoding.Latin1,
            "windows-1252" or "cp1252" => Encoding.GetEncoding(1252),
            _ => Encoding.UTF8
        };
}
