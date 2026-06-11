using System.Text.Json;

namespace CrfDemo.Corpus;

public static class CorpusLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static CorpusDocument Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<CorpusDocument>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Corpus file is empty or invalid JSON.");
    }
}
