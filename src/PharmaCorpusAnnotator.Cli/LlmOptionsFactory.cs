using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Cli;

public static class LlmOptionsFactory
{
    public static LlmOptions FromEnvironment(string? attemptsOutputPath = null)
    {
        var model = Env("LLM_MODEL", "qwen3:14b")!;
        var baseUrlRaw = Env("LLM_BASE_URL", "http://localhost:11434")!;
        var apiKey = Env("LLM_API_KEY", "ollama")!;
        var ignoreSsl = bool.TryParse(Env("LLM_IGNORE_SSL_ERRORS", "false"), out var ssl) && ssl;
        var username = Env("LLM_USERNAME", null);
        var password = Env("LLM_PASSWORD", null);
        var retryCount = int.TryParse(Env("LLM_RETRY_COUNT", "5"), out var rc) ? rc : 5;
        var timeoutMin = double.TryParse(Env("LLM_TIMEOUT_MINUTES", "30"), out var tm) ? tm : 30;

        var baseEndpoint = NormalizeEndpoint(baseUrlRaw);

        return new LlmOptions(
            ModelId: model,
            BaseEndpoint: baseEndpoint,
            ApiKey: apiKey,
            IgnoreSslErrors: ignoreSsl,
            Username: username,
            Password: password,
            RetryCount: retryCount,
            Timeout: TimeSpan.FromMinutes(timeoutMin),
            AttemptsOutputPath: attemptsOutputPath);
    }

    private static string? Env(string name, string? fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;

    public static Uri NormalizeEndpoint(string raw)
    {
        raw = raw.TrimEnd('/');

        // If it's the default Ollama base URL without a path, append /v1
        if (!raw.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw + "/v1";
        }

        return new Uri(raw);
    }
}
