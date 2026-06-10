using System.Text.Json;
using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Output;

public sealed class FailedRecordWriter : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = JsonSerializerOptions.Web;

    private readonly string _path;
    private StreamWriter? _writer;

    public FailedRecordWriter(string path)
    {
        _path = path;
    }

    public void Write(FailedRecord record)
    {
        EnsureOpen();
        _writer!.WriteLine(JsonSerializer.Serialize(record, JsonOpts));
        _writer.Flush();
    }

    private void EnsureOpen()
    {
        if (_writer is not null) return;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        _writer = new StreamWriter(_path, append: true);
    }

    public void Dispose() => _writer?.Dispose();
}
