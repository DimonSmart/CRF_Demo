using System.Reflection;
using System.Text.Json;
using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Llm;

public sealed class PharmaAnnotationPromptBuilder
{
    private static readonly JsonSerializerOptions SerOpts = JsonSerializerOptions.Web;
    private static readonly Lazy<string> SystemPromptText = new(LoadSystemPrompt);

    public string GetSystemPrompt() => SystemPromptText.Value;

    public string BuildUserPrompt(PharmaAnnotationModelRequest request)
    {
        var payload = new
        {
            tokens = request.Tokens.Select(t => t.Text),
            allowedLabels = request.AllowedLabels,
        };
        return JsonSerializer.Serialize(payload, SerOpts);
    }

    private static string LoadSystemPrompt()
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("pharma-annotation.system.md", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Embedded system prompt resource not found.");

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not open embedded resource '{resourceName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
