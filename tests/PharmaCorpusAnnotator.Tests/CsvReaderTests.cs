using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PharmaCorpusAnnotator.Core.Csv;
using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Tests;

public class CsvReaderTests
{
    private static readonly string SampleCsv =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.csv");

    private CsvSourceOptions DefaultOptions(string? textColumn = null) =>
        new(
            InputPath: SampleCsv,
            SourceKey: "test-source",
            TextColumn: textColumn ?? "Nombre del producto farmacéutico",
            Delimiter: ";",
            Encoding: "utf-8-sig",
            Skip: 0,
            MaxRows: null);

    private static CsvPharmaSourceReader CreateReader() =>
        new(NullLogger<CsvPharmaSourceReader>.Instance);

    [Fact]
    public async Task Reads_Utf8Bom_Correctly()
    {
        var reader = CreateReader();
        var rows = new List<PharmaSourceRow>();
        await foreach (var r in reader.ReadAsync(DefaultOptions(), TestContext.Current.CancellationToken))
            rows.Add(r);
        rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task Uses_Semicolon_Delimiter()
    {
        var reader = CreateReader();
        var rows = new List<PharmaSourceRow>();
        await foreach (var r in reader.ReadAsync(DefaultOptions(), TestContext.Current.CancellationToken))
            rows.Add(r);
        rows[0].Text.Should().Be("aceite salicilado 50 mg/ml solucion cutanea 100 ml 1 frasco");
    }

    [Fact]
    public async Task FindsTextColumn_NombreDelProducto()
    {
        var reader = CreateReader();
        var rows = new List<PharmaSourceRow>();
        await foreach (var r in reader.ReadAsync(DefaultOptions(), TestContext.Current.CancellationToken))
            rows.Add(r);
        rows[0].Text.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FirstDataRow_HasRowNumber2()
    {
        var reader = CreateReader();
        var rows = new List<PharmaSourceRow>();
        await foreach (var r in reader.ReadAsync(DefaultOptions(), TestContext.Current.CancellationToken))
            rows.Add(r);
        rows[0].RowNumber.Should().Be(2);
    }

    [Fact]
    public async Task Rows_DoNotExposeContext()
    {
        var reader = CreateReader();
        var rows = new List<PharmaSourceRow>();
        await foreach (var r in reader.ReadAsync(DefaultOptions(), TestContext.Current.CancellationToken))
            rows.Add(r);
        var first = rows[0];
        typeof(PharmaSourceRow).GetProperty("Context").Should().BeNull();
    }

    [Fact]
    public async Task MissingTextColumn_Throws()
    {
        var reader = CreateReader();
        var opts = DefaultOptions(textColumn: "Nonexistent Column");
        Func<Task> act = async () =>
        {
            await foreach (var _ in reader.ReadAsync(opts, TestContext.Current.CancellationToken)) { }
        };
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Nonexistent Column*");
    }

    [Fact]
    public async Task DoesNotRequireContextColumns()
    {
        var reader = CreateReader();
        var rows = new List<PharmaSourceRow>();
        await foreach (var r in reader.ReadAsync(DefaultOptions(), TestContext.Current.CancellationToken))
            rows.Add(r);
        rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task MaxRows_LimitsOutput()
    {
        var reader = CreateReader();
        var opts = DefaultOptions() with { MaxRows = 2 };
        var rows = new List<PharmaSourceRow>();
        await foreach (var r in reader.ReadAsync(opts, TestContext.Current.CancellationToken))
            rows.Add(r);
        rows.Should().HaveCount(2);
    }
}
