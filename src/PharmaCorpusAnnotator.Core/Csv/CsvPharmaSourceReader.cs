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

        var missingContext = options.ContextColumns
            .Where(c => !headers.Contains(c))
            .ToList();
        foreach (var col in missingContext)
            _logger.LogWarning("Context column '{Column}' not found in CSV and will be skipped.", col);

        var availableContext = options.ContextColumns.Where(headers.Contains).ToList();

        long physicalRow = 1; // header was row 1
        long skipped = 0;
        long processed = 0;

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            physicalRow++;

            if (options.Skip > 0 && skipped < options.Skip)
            {
                skipped++;
                continue;
            }

            if (options.MaxRows.HasValue && processed >= options.MaxRows.Value)
                yield break;

            string text;
            try
            {
                text = csv.GetField(options.TextColumn) ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read text column at row {Row}, skipping.", physicalRow);
                continue;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty text at row {Row}, skipping.", physicalRow);
                continue;
            }

            var context = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var col in availableContext)
            {
                var val = csv.GetField(col);
                if (val is not null)
                    context[col] = val;
            }

            processed++;
            yield return new PharmaSourceRow(
                options.SourceKey,
                fileName,
                physicalRow,
                options.TextColumn,
                text.Trim(),
                context);
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
