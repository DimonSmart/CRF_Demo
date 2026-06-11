using Microsoft.Extensions.Configuration;
using PharmaCorpusAnnotator.Core.Models;
using System.Text.RegularExpressions;

namespace PharmaCorpusAnnotator.Cli;

public static class LlmOptionsFactory
{
    public static LlmOptions FromEnvironment(string? attemptsOutputPath = null, string? profileName = null)
    {
        var configuration = BuildConfiguration();
        var explicitProfileName = NonEmpty(profileName);
        var activeProfileName = explicitProfileName
            ?? Env("LLM_PROFILE", null)
            ?? configuration["Llm:ActiveProfile"]
            ?? "ollama";
        var useEnvironmentOverrides = explicitProfileName is null;

        var profile = FindProfile(configuration, activeProfileName);

        var model = EnvOverride("LLM_MODEL", useEnvironmentOverrides)
            ?? profile.Model
            ?? "qwen3:14b";
        var baseUrlRaw = EnvOverride("LLM_BASE_URL", useEnvironmentOverrides)
            ?? profile.BaseUrl
            ?? "http://localhost:11434";
        var apiKey = ResolveSecretReference(
                EnvOverride("LLM_API_KEY", useEnvironmentOverrides) ?? profile.ApiKey ?? "ollama",
                configuration)
            ?? throw new InvalidOperationException("LLM API key is empty.");
        var ignoreSsl = GetBool("LLM_IGNORE_SSL_ERRORS", useEnvironmentOverrides, profile.IgnoreSslErrors, false);
        var username = EnvOverride("LLM_USERNAME", useEnvironmentOverrides) ?? profile.Username;
        var password = ResolveSecretReference(
            EnvOverride("LLM_PASSWORD", useEnvironmentOverrides) ?? profile.Password,
            configuration);
        var retryCount = GetInt("LLM_RETRY_COUNT", useEnvironmentOverrides, profile.RetryCount, 5);
        var timeoutMin = GetDouble("LLM_TIMEOUT_MINUTES", useEnvironmentOverrides, profile.TimeoutMinutes, 30);

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

    private static string? EnvOverride(string name, bool enabled) =>
        enabled ? Env(name, null) : null;

    private static string? NonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static IConfigurationRoot BuildConfiguration()
    {
        var configPath = Env("LLM_CONFIG_PATH", "llmsettings.json")!;

        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(ResolveConfigPath(configPath), optional: true, reloadOnChange: false)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string ResolveConfigPath(string configPath)
    {
        if (Path.IsPathRooted(configPath))
            return configPath;

        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, configPath);
            if (File.Exists(candidate))
                return candidate;
        }

        return configPath;
    }

    private static LlmProfileOptions FindProfile(IConfiguration configuration, string activeProfileName)
    {
        var profiles = configuration
            .GetSection("Llm:Profiles")
            .Get<LlmProfileOptions[]>()
            ?? [];

        var profileByName = profiles.FirstOrDefault(
            p => string.Equals(p.Name, activeProfileName, StringComparison.OrdinalIgnoreCase));
        if (profileByName is not null)
            return profileByName;

        if (int.TryParse(activeProfileName, out var profileNumber)
            && profileNumber >= 1
            && profileNumber <= profiles.Length)
        {
            return profiles[profileNumber - 1];
        }

        return new LlmProfileOptions { Name = activeProfileName };
    }

    private static string? ResolveSecretReference(string? value, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var match = Regex.Match(value, @"^%([A-Za-z0-9_.:-]+)%$");
        if (!match.Success)
            return value;

        var secretName = match.Groups[1].Value;
        var secretValue = configuration[secretName];
        if (string.IsNullOrWhiteSpace(secretValue))
            throw new InvalidOperationException(
                $"LLM secret '{secretName}' was not found. Set it with user-secrets or an environment variable.");

        return secretValue;
    }

    private static bool GetBool(string envName, bool useEnvironmentOverrides, bool? profileValue, bool fallback)
    {
        var raw = EnvOverride(envName, useEnvironmentOverrides);
        return raw is not null && bool.TryParse(raw, out var envValue)
            ? envValue
            : profileValue ?? fallback;
    }

    private static int GetInt(string envName, bool useEnvironmentOverrides, int? profileValue, int fallback)
    {
        var raw = EnvOverride(envName, useEnvironmentOverrides);
        return raw is not null && int.TryParse(raw, out var envValue)
            ? envValue
            : profileValue ?? fallback;
    }

    private static double GetDouble(string envName, bool useEnvironmentOverrides, double? profileValue, double fallback)
    {
        var raw = EnvOverride(envName, useEnvironmentOverrides);
        return raw is not null && double.TryParse(raw, out var envValue)
            ? envValue
            : profileValue ?? fallback;
    }

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
