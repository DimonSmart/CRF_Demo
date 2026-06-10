using System.Text.Json;
using FluentAssertions;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Output;
using Microsoft.Extensions.Logging;

namespace PharmaCorpusAnnotator.Tests;

public class CorpusWriterTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerOptions.Web) { WriteIndented = true };
    private readonly string _tmpDir;

    public CorpusWriterTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "pharma-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tmpDir);
    }

    private static ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(_ => { });

    private static PharmaCorpusSourceHeader MakeHeader(string sourceKey = "test-source") =>
        new(sourceKey, "test.csv", "csv", "utf-8-sig", ";", "Nombre del producto farmacéutico");

    private static PharmaCorpusRecord MakeRecord(long rowNum) =>
        new(rowNum, $"product text {rowNum}",
            new PharmaAnnotationResponse(
                [],
                new NormalizedPharmaItem(null, null, null, [], null, null, null, null, null, null, null, null),
                new AnnotationQuality(null, [])));

    [Fact]
    public void WritesSourceMetadata_Once()
    {
        var path = Path.Combine(_tmpDir, "corpus.json");
        using var writer = new PharmaCorpusWriter(path, null, CreateLoggerFactory());
        writer.SetSource(MakeHeader());
        writer.AddRecord(MakeRecord(2));
        writer.AddRecord(MakeRecord(3));
        writer.Complete();

        using var f = File.OpenRead(path);
        var doc = JsonSerializer.Deserialize<PharmaCorpusDocument>(f, JsonOpts)!;

        doc.Sources.Should().HaveCount(1);
        doc.Sources[0].Source.FileName.Should().Be("test.csv");
        doc.Sources[0].Records.Should().HaveCount(2);
    }

    [Fact]
    public void DoesNotRepeat_FileName_PerRecord()
    {
        var path = Path.Combine(_tmpDir, "corpus.json");
        using var writer = new PharmaCorpusWriter(path, null, CreateLoggerFactory());
        writer.SetSource(MakeHeader());
        writer.AddRecord(MakeRecord(2));
        writer.Complete();

        var json = File.ReadAllText(path);
        // "fileName" should appear exactly once (in source header)
        var count = 0;
        int idx = 0;
        while ((idx = json.IndexOf("\"fileName\"", idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            idx++;
        }
        count.Should().Be(1);
    }

    [Fact]
    public void DoesNotWrite_SourceId()
    {
        var path = Path.Combine(_tmpDir, "corpus.json");
        using var writer = new PharmaCorpusWriter(path, null, CreateLoggerFactory());
        writer.SetSource(MakeHeader());
        writer.AddRecord(MakeRecord(2));
        writer.Complete();

        var json = File.ReadAllText(path);
        json.Should().NotContain("\"sourceId\"");
    }

    [Fact]
    public void SerializedJson_DoesNotContainContextOrContextColumns()
    {
        var path = Path.Combine(_tmpDir, "corpus.json");
        using var writer = new PharmaCorpusWriter(path, null, CreateLoggerFactory());
        writer.SetSource(MakeHeader());
        writer.AddRecord(MakeRecord(2));
        writer.Complete();

        var json = File.ReadAllText(path);
        json.Should().NotContain("\"context\"");
        json.Should().NotContain("\"contextColumns\"");
    }

    [Fact]
    public void WritesMultipleRecords_UnderOneSourceBlock()
    {
        var path = Path.Combine(_tmpDir, "corpus.json");
        using var writer = new PharmaCorpusWriter(path, null, CreateLoggerFactory());
        writer.SetSource(MakeHeader());
        for (int i = 2; i < 7; i++)
            writer.AddRecord(MakeRecord(i));
        writer.Complete();

        using var f = File.OpenRead(path);
        var doc = JsonSerializer.Deserialize<PharmaCorpusDocument>(f, JsonOpts)!;
        doc.Sources.Should().HaveCount(1);
        doc.Sources[0].Records.Should().HaveCount(5);
    }

    [Fact]
    public void Resume_BySourceKeyAndRowNumber()
    {
        var path = Path.Combine(_tmpDir, "corpus.json");

        // First run: write rows 2 and 3
        using (var writer = new PharmaCorpusWriter(path, null, CreateLoggerFactory()))
        {
            writer.SetSource(MakeHeader());
            writer.AddRecord(MakeRecord(2));
            writer.AddRecord(MakeRecord(3));
            writer.Complete();
        }

        var existing = PharmaCorpusWriter.TryReadExisting(path)!;
        using (var writer = new PharmaCorpusWriter(path, existing, CreateLoggerFactory()))
        {
            writer.SetSource(MakeHeader());
            var keys = writer.GetProcessedKeys("test-source");
            keys.Should().Contain("test-source:2");
            keys.Should().Contain("test-source:3");
            keys.Should().NotContain("test-source:4");
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmpDir, recursive: true); }
        catch { /* best effort */ }
    }
}
