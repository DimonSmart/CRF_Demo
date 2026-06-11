using Microsoft.Extensions.Logging;
using PharmaCorpusAnnotator.Core.Csv;
using PharmaCorpusAnnotator.Core.Interfaces;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Output;
using PharmaCorpusAnnotator.Core.Tokenization;

namespace PharmaCorpusAnnotator.Core.Pipeline;

public sealed class AnnotationRunner
{
    private readonly CsvPharmaSourceReader _csvReader;
    private readonly PharmaTokenizer _tokenizer;
    private readonly IPharmaAnnotationModelClient _modelClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AnnotationRunner> _logger;

    public AnnotationRunner(
        CsvPharmaSourceReader csvReader,
        PharmaTokenizer tokenizer,
        IPharmaAnnotationModelClient modelClient,
        ILoggerFactory loggerFactory)
    {
        _csvReader = csvReader;
        _tokenizer = tokenizer;
        _modelClient = modelClient;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AnnotationRunner>();
    }

    public async Task RunAsync(
        AnnotationRunnerOptions options,
        CancellationToken cancellationToken = default)
    {
        var csvOpts = options.CsvOptions;

        var existingDoc = options.Resume
            ? PharmaCorpusWriter.TryReadExisting(options.OutputPath)
            : null;

        // Validate source metadata consistency when resuming
        if (existingDoc is not null && options.Resume)
        {
            var existingBlock = existingDoc.Sources
                .FirstOrDefault(s => s.Source.SourceKey == csvOpts.SourceKey);

            if (existingBlock is not null)
            {
                var h = existingBlock.Source;
                if (h.TextColumn != csvOpts.TextColumn)
                    throw new InvalidOperationException(
                        $"Source key \"{csvOpts.SourceKey}\" already exists in output but textColumn differs.\n" +
                        $"Existing: {h.TextColumn}\n" +
                        $"Current:  {csvOpts.TextColumn}");

                if (h.Delimiter != csvOpts.Delimiter)
                    throw new InvalidOperationException(
                        $"Source key \"{csvOpts.SourceKey}\" already exists but delimiter differs.\n" +
                        $"Existing: {h.Delimiter}\nCurrent: {csvOpts.Delimiter}");

                if (h.RequiredNonEmptyColumn != csvOpts.RequiredNonEmptyColumn)
                    throw new InvalidOperationException(
                        $"Source key \"{csvOpts.SourceKey}\" already exists but requiredNonEmptyColumn differs.\n" +
                        $"Existing: {h.RequiredNonEmptyColumn ?? "(none)"}\n" +
                        $"Current:  {csvOpts.RequiredNonEmptyColumn ?? "(none)"}");
            }
        }

        using var corpusWriter = new PharmaCorpusWriter(options.OutputPath, existingDoc, _loggerFactory);
        using var failedWriter = new FailedRecordWriter(options.FailedOutputPath);

        var sourceHeader = new PharmaCorpusSourceHeader(
            csvOpts.SourceKey,
            Path.GetFileName(csvOpts.InputPath),
            "csv",
            csvOpts.Encoding,
            csvOpts.Delimiter,
            csvOpts.TextColumn,
            csvOpts.RequiredNonEmptyColumn);

        corpusWriter.SetSource(sourceHeader);

        var processedKeys = options.Resume
            ? corpusWriter.GetProcessedKeys(csvOpts.SourceKey)
            : new HashSet<string>(StringComparer.Ordinal);

        long processed = 0, success = 0, failed = 0, skipped = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation("Starting annotation run");
        _logger.LogInformation("Input:          {Path}", csvOpts.InputPath);
        _logger.LogInformation("Output:         {Path}", options.OutputPath);
        _logger.LogInformation("Source key:     {Key}", csvOpts.SourceKey);
        _logger.LogInformation("Text column:    {Col}", csvOpts.TextColumn);
        _logger.LogInformation("Required col:   {Col}", csvOpts.RequiredNonEmptyColumn ?? "(none)");
        _logger.LogInformation("Max rows:       {Max}", csvOpts.MaxRows?.ToString() ?? "all");
        _logger.LogInformation("Resume:         {Resume}", options.Resume);
        _logger.LogInformation("Dry run:        {DryRun}", options.DryRun);

        var readOpts = csvOpts with { MaxRows = null };

        await using var rows = _csvReader
            .ReadAsync(readOpts, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        var hasRow = await rows.MoveNextAsync();
        while (hasRow)
        {
            var skipResult = await SkipProcessedRowsAsync(rows, rows.Current, processedKeys);
            skipped += skipResult.Skipped;

            if (!skipResult.HasPendingRow)
                break;

            var row = skipResult.PendingRow!;

            if (csvOpts.MaxRows.HasValue && processed >= csvOpts.MaxRows.Value)
                break;

            var tokens = _tokenizer.Tokenize(row.Text);
            processed++;

            if (options.Verbose)
            {
                _logger.LogInformation("Row {Row}: {Text}", row.RowNumber, row.Text);
                _logger.LogInformation("  Tokens: {Tokens}", string.Join(" | ", tokens.Select(t => t.Text)));
            }
            else
            {
                _logger.LogInformation("Processing row {Row}", row.RowNumber);
            }

            if (options.DryRun)
            {
                Console.WriteLine($"[DRY-RUN] Row {row.RowNumber}: {row.Text}");
                Console.WriteLine($"  Tokens: {string.Join(" | ", tokens.Select(t => t.Text))}");
                continue;
            }

            var request = new PharmaAnnotationModelRequest(
                Language: "es",
                SourceKey: row.SourceKey,
                RowNumber: row.RowNumber,
                Text: row.Text,
                Tokens: tokens);

            try
            {
                var response = await _modelClient.AnnotateAsync(request, cancellationToken);
                var record = new PharmaCorpusRecord(row.RowNumber, row.Text, response);
                corpusWriter.AddRecord(record);
                success++;

                if (options.Verbose)
                    _logger.LogInformation("  -> Success");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (AnnotationFailedException afex)
            {
                failed++;
                _logger.LogError("Row {Row} failed after {Attempts} attempts: {Error}",
                    row.RowNumber, afex.Attempts, afex.ValidationError);

                failedWriter.Write(new FailedRecord(
                    row.SourceKey,
                    row.RowNumber,
                    row.Text,
                    new FailedRecordError("llm_contract_invalid", afex.ValidationError, afex.Attempts)));
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Row {Row} failed with unexpected error", row.RowNumber);

                failedWriter.Write(new FailedRecord(
                    row.SourceKey,
                    row.RowNumber,
                    row.Text,
                    new FailedRecordError("llm_call_failed", ex.Message, 1)));
            }

            hasRow = await rows.MoveNextAsync();
        }

        if (!options.DryRun)
            corpusWriter.Complete();

        _logger.LogInformation(
            "Done. Elapsed: {Elapsed}. Processed: {P}, Success: {S}, Failed: {F}, Skipped: {Sk}",
            sw.Elapsed, processed, success, failed, skipped);
    }

    private async ValueTask<SkipProcessedRowsResult> SkipProcessedRowsAsync(
        IAsyncEnumerator<PharmaSourceRow> rows,
        PharmaSourceRow firstRow,
        IReadOnlySet<string> processedKeys)
    {
        var skipped = 0L;
        var firstSkippedRow = 0L;
        var lastSkippedRow = 0L;
        var row = firstRow;

        while (processedKeys.Contains(GetProcessedKey(row)))
        {
            skipped++;
            firstSkippedRow = firstSkippedRow == 0 ? row.RowNumber : firstSkippedRow;
            lastSkippedRow = row.RowNumber;

            if (!await rows.MoveNextAsync())
            {
                LogSkippedRows(skipped, firstSkippedRow, lastSkippedRow);
                return new SkipProcessedRowsResult(null, skipped);
            }

            row = rows.Current;
        }

        LogSkippedRows(skipped, firstSkippedRow, lastSkippedRow);
        return new SkipProcessedRowsResult(row, skipped);
    }

    private static string GetProcessedKey(PharmaSourceRow row) => $"{row.SourceKey}:{row.RowNumber}";

    private void LogSkippedRows(long skipped, long firstRow, long lastRow)
    {
        if (skipped == 0)
            return;

        _logger.LogDebug(
            "Skipped {Count} already processed rows ({FirstRow}-{LastRow})",
            skipped,
            firstRow,
            lastRow);
    }

    private readonly record struct SkipProcessedRowsResult(PharmaSourceRow? PendingRow, long Skipped)
    {
        public bool HasPendingRow => PendingRow is not null;
    }

}
