namespace PharmaCorpusAnnotator.Cli;

public sealed class LlmProfileOptions
{
    public string Name { get; init; } = "";
    public string? BaseUrl { get; init; }
    public string? Model { get; init; }
    public string? ApiKey { get; init; }
    public bool? IgnoreSslErrors { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public int? RetryCount { get; init; }
    public double? TimeoutMinutes { get; init; }
}
