using System.Text.Json.Serialization;

namespace CrfDemo.Corpus;

public sealed class CorpusDocument
{
    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; init; }

    [JsonPropertyName("annotationSchema")]
    public AnnotationSchema? AnnotationSchema { get; init; }

    [JsonPropertyName("sources")]
    public IReadOnlyList<CorpusSource> Sources { get; init; } = Array.Empty<CorpusSource>();
}

public sealed class AnnotationSchema
{
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("labels")]
    public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();
}

public sealed class CorpusSource
{
    [JsonPropertyName("source")]
    public CorpusSourceHeader? Source { get; init; }

    [JsonPropertyName("records")]
    public IReadOnlyList<CorpusRecord> Records { get; init; } = Array.Empty<CorpusRecord>();
}

public sealed class CorpusSourceHeader
{
    [JsonPropertyName("sourceKey")]
    public string? SourceKey { get; init; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }
}

public sealed class CorpusRecord
{
    [JsonPropertyName("rowNumber")]
    public int RowNumber { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("annotation")]
    public CorpusAnnotation? Annotation { get; init; }
}

public sealed class CorpusAnnotation
{
    [JsonPropertyName("tokens")]
    public IReadOnlyList<CorpusToken> Tokens { get; init; } = Array.Empty<CorpusToken>();

    [JsonPropertyName("normalized")]
    public object? Normalized { get; init; }

    [JsonPropertyName("quality")]
    public CorpusQuality? Quality { get; init; }
}

public sealed class CorpusToken
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";
}

public sealed class CorpusQuality
{
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
