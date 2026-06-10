namespace PharmaCorpusAnnotator.Core.Models;

public sealed record LlmOptions(
    string ModelId,
    Uri BaseEndpoint,
    string ApiKey,
    bool IgnoreSslErrors,
    string? Username,
    string? Password,
    int RetryCount,
    TimeSpan Timeout);
