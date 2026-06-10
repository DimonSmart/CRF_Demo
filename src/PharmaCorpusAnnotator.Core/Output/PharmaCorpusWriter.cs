using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PharmaCorpusAnnotator.Core.Labeling;
using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Output;

public sealed class PharmaCorpusWriter : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerOptions.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _outputPath;
    private readonly string _tmpPath;
    private readonly ILogger<PharmaCorpusWriter> _logger;

    private PharmaCorpusDocument _document;
    private readonly List<PharmaCorpusRecord> _currentSourceRecords;
    private PharmaCorpusSourceHeader? _currentSourceHeader;

    // Flush every N records to the temp file
    private const int FlushInterval = 100;
    private int _unflushed;

    public PharmaCorpusWriter(
        string outputPath,
        PharmaCorpusDocument? existingDocument,
        ILoggerFactory loggerFactory)
    {
        _outputPath = outputPath;
        _tmpPath = outputPath + ".tmp";
        _logger = loggerFactory.CreateLogger<PharmaCorpusWriter>();
        _currentSourceRecords = new List<PharmaCorpusRecord>();

        _document = existingDocument ?? new PharmaCorpusDocument(
            SchemaVersion: "1.0",
            CreatedAt: DateTimeOffset.UtcNow,
            AnnotationSchema: new AnnotationSchema("es", PharmaAnnotationLabels.All),
            Sources: new List<PharmaCorpusSourceBlock>());
    }

    public void SetSource(PharmaCorpusSourceHeader header)
    {
        if (_currentSourceHeader is not null &&
            _currentSourceHeader.SourceKey != header.SourceKey)
        {
            CommitCurrentSource();
        }

        _currentSourceHeader = header;

        // If the source already exists in the document (resume), load its records
        var existing = ((List<PharmaCorpusSourceBlock>)_document.Sources)
            .FirstOrDefault(s => s.Source.SourceKey == header.SourceKey);

        if (existing is not null)
        {
            _currentSourceRecords.Clear();
            _currentSourceRecords.AddRange(existing.Records);
            ((List<PharmaCorpusSourceBlock>)_document.Sources).Remove(existing);
        }
        else
        {
            _currentSourceRecords.Clear();
        }
    }

    public void AddRecord(PharmaCorpusRecord record)
    {
        _currentSourceRecords.Add(record);
        _unflushed++;

        if (_unflushed >= FlushInterval)
        {
            FlushToTemp();
            _unflushed = 0;
        }
    }

    public HashSet<string> GetProcessedKeys(string sourceKey)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var existing = _document.Sources.FirstOrDefault(s => s.Source.SourceKey == sourceKey);
        if (existing is not null)
        {
            foreach (var r in existing.Records)
                keys.Add($"{sourceKey}:{r.RowNumber}");
        }
        // Also include current in-progress records
        foreach (var r in _currentSourceRecords)
            keys.Add($"{sourceKey}:{r.RowNumber}");
        return keys;
    }

    public void Complete()
    {
        CommitCurrentSource();
        WriteDocumentToTemp(_document);
        AtomicMove();
    }

    private void CommitCurrentSource()
    {
        if (_currentSourceHeader is null) return;

        var sources = (List<PharmaCorpusSourceBlock>)_document.Sources;
        var block = new PharmaCorpusSourceBlock(
            _currentSourceHeader,
            _currentSourceRecords.ToList());
        sources.Add(block);
        _currentSourceRecords.Clear();
        _currentSourceHeader = null;
    }

    private void FlushToTemp()
    {
        if (_currentSourceHeader is null) return;

        var sources = new List<PharmaCorpusSourceBlock>(_document.Sources)
        {
            new PharmaCorpusSourceBlock(_currentSourceHeader, _currentSourceRecords.ToList())
        };
        WriteDocumentToTemp(_document with { Sources = sources });
        _logger.LogDebug("Flushed {Count} records to temp file.", _currentSourceRecords.Count);
    }

    private void WriteDocumentToTemp(PharmaCorpusDocument doc)
    {
        var dir = Path.GetDirectoryName(_tmpPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var f = File.Create(_tmpPath);
        JsonSerializer.Serialize(f, doc, JsonOpts);
    }

    private void AtomicMove()
    {
        if (!File.Exists(_tmpPath)) return;
        var dir = Path.GetDirectoryName(_outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.Move(_tmpPath, _outputPath, overwrite: true);
        _logger.LogInformation("Corpus written to {Path}", _outputPath);
    }

    public static PharmaCorpusDocument? TryReadExisting(string outputPath)
    {
        if (!File.Exists(outputPath)) return null;
        try
        {
            using var f = File.OpenRead(outputPath);
            return JsonSerializer.Deserialize<PharmaCorpusDocument>(f, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (File.Exists(_tmpPath))
        {
            try { File.Delete(_tmpPath); }
            catch { /* best effort */ }
        }
    }
}
