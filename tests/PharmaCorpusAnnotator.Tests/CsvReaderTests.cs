using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PharmaCorpusAnnotator.Core.Csv;
using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Tests;

public class CsvReaderTests
{
    private const string RequiredColumn = "Principio activo o asociación de principios activos";

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

    [Fact]
    public async Task RequiredNonEmptyColumn_Disabled_ReadsRowsWithEmptyRequiredValue()
    {
        var path = await WriteTempCsvAsync(
            $"Nombre del producto farmacéutico;{RequiredColumn}" + Environment.NewLine +
            "producto a;aaa" + Environment.NewLine +
            "producto b;" + Environment.NewLine +
            "producto c;ccc" + Environment.NewLine);

        var reader = CreateReader();
        var rows = new List<PharmaSourceRow>();
        await foreach (var r in reader.ReadAsync(DefaultOptions() with { InputPath = path }, TestContext.Current.CancellationToken))
            rows.Add(r);

        rows.Select(r => r.Text).Should().Equal("producto a", "producto b", "producto c");
    }

    [Fact]
    public async Task RequiredNonEmptyColumn_Enabled_SkipsRowsWithEmptyRequiredValue()
    {
        var path = await WriteTempCsvAsync(
            $"Nombre del producto farmacéutico;{RequiredColumn}" + Environment.NewLine +
            "producto a;aaa" + Environment.NewLine +
            "producto b;" + Environment.NewLine +
            "producto c;ccc" + Environment.NewLine);

        var reader = CreateReader();
        var rows = new List<PharmaSourceRow>();
        var opts = DefaultOptions() with
        {
            InputPath = path,
            RequiredNonEmptyColumn = RequiredColumn,
        };

        await foreach (var r in reader.ReadAsync(opts, TestContext.Current.CancellationToken))
            rows.Add(r);

        rows.Select(r => r.Text).Should().Equal("producto a", "producto c");
    }

    [Fact]
    public async Task MissingRequiredNonEmptyColumn_ThrowsBeforeRows()
    {
        var path = await WriteTempCsvAsync(
            "Nombre del producto farmacéutico;Otra columna" + Environment.NewLine +
            "producto a;aaa" + Environment.NewLine);

        var reader = CreateReader();
        var opts = DefaultOptions() with
        {
            InputPath = path,
            RequiredNonEmptyColumn = RequiredColumn,
        };

        Func<Task> act = async () =>
        {
            await foreach (var _ in reader.ReadAsync(opts, TestContext.Current.CancellationToken)) { }
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Required non-empty column not found: {RequiredColumn}");
    }

    [Fact]
    public async Task RequiredNonEmptyColumn_TreatsWhitespaceAsEmpty()
    {
        var path = await WriteTempCsvAsync(
            $"Nombre del producto farmacéutico;{RequiredColumn}" + Environment.NewLine +
            "producto a;   " + Environment.NewLine +
            "producto b;bbb" + Environment.NewLine);

        var reader = CreateReader();
        var rows = new List<PharmaSourceRow>();
        var opts = DefaultOptions() with
        {
            InputPath = path,
            RequiredNonEmptyColumn = RequiredColumn,
        };

        await foreach (var r in reader.ReadAsync(opts, TestContext.Current.CancellationToken))
            rows.Add(r);

        rows.Select(r => r.Text).Should().Equal("producto b");
    }

    [Fact]
    public async Task MaxRows_LimitsRowsAcceptedAfterRequiredNonEmptyColumnFilter()
    {
        var path = await WriteTempCsvAsync(
            $"Nombre del producto farmacéutico;{RequiredColumn}" + Environment.NewLine +
            "producto a;" + Environment.NewLine +
            "producto b;bbb" + Environment.NewLine +
            "producto c;" + Environment.NewLine +
            "producto d;ddd" + Environment.NewLine +
            "producto e;eee" + Environment.NewLine);

        var reader = CreateReader();
        var rows = new List<PharmaSourceRow>();
        var opts = DefaultOptions() with
        {
            InputPath = path,
            RequiredNonEmptyColumn = RequiredColumn,
            MaxRows = 2,
        };

        await foreach (var r in reader.ReadAsync(opts, TestContext.Current.CancellationToken))
            rows.Add(r);

        rows.Select(r => r.Text).Should().Equal("producto b", "producto d");
    }

    [Fact]
    public async Task Skip_AppliesToRowsAcceptedAfterRequiredNonEmptyColumnFilter()
    {
        var path = await WriteTempCsvAsync(
            $"Nombre del producto farmacéutico;{RequiredColumn}" + Environment.NewLine +
            "producto a;" + Environment.NewLine +
            "producto b;bbb" + Environment.NewLine +
            "producto c;ccc" + Environment.NewLine +
            "producto d;ddd" + Environment.NewLine);

        var reader = CreateReader();
        var rows = new List<PharmaSourceRow>();
        var opts = DefaultOptions() with
        {
            InputPath = path,
            RequiredNonEmptyColumn = RequiredColumn,
            Skip = 1,
            MaxRows = 1,
        };

        await foreach (var r in reader.ReadAsync(opts, TestContext.Current.CancellationToken))
            rows.Add(r);

        rows.Select(r => r.Text).Should().Equal("producto c");
    }

    private static async Task<string> WriteTempCsvAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pharma-csv-reader-{Guid.NewGuid()}.csv");
        await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
        return path;
    }
}
