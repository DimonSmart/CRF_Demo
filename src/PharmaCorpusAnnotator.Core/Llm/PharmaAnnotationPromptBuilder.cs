using System.Text.Json;
using Microsoft.Extensions.AI;
using PharmaCorpusAnnotator.Core.Labeling;
using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Llm;

public sealed class PharmaAnnotationPromptBuilder
{
    private static readonly JsonSerializerOptions SerOpts = JsonSerializerOptions.Web;

    public List<ChatMessage> BuildMessages(
        PharmaAnnotationModelRequest request,
        IReadOnlyList<string> previousErrors)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, BuildSystemPrompt()),
            new(ChatRole.User, BuildUserPrompt(request)),
        };

        if (previousErrors.Count > 0)
            messages.Add(new ChatMessage(ChatRole.User, BuildRepairPrompt(request.Tokens.Count, previousErrors)));

        return messages;
    }

    public string BuildSystemPrompt()
    {
        return string.Join('\n',
            "You label Spanish pharmaceutical product-name tokens.",
            "",
            "Input is JSON:",
            "{",
            "  \"tokens\": [\"...\"],",
            "  \"tokenCount\": 0",
            "}",
            "",
            "Return only JSON:",
            "{\"labels\":[\"...\"]}",
            "",
            "Rules:",
            "- labels.length must equal tokenCount.",
            "- labels[i] is the BIO label for tokens[i].",
            "- Use only the labels defined below.",
            "- Use O when the token is not an entity.",
            "- Use B-X for the first token of an entity.",
            "- Use I-X only after B-X or I-X of the same entity.",
            "- Do not return tokens, indexes, spans, confidence, warnings or explanations.",
            "",
            "Labels:",
            string.Join('\n', PharmaAnnotationLabels.All),
            "",
            "Entities:",
            "- ACTIVE_INGREDIENT: active substance, e.g. ibuprofeno, captopril, amoxicilina acido clavulanico.",
            "- STRENGTH: dosage/concentration, e.g. 600 mg, 4 mg/ml, 875 mg 125 mg.",
            "- DOSE_FORM: pharmaceutical form, e.g. comprimidos recubiertos con pelicula, suspension, solucion, colirio.",
            "- ROUTE: administration route, e.g. oral, topica, oftalmica.",
            "- PACKAGE_VOLUME: package volume, e.g. 100 ml, 30 g, 2.5 ml.",
            "- PACKAGE_QUANTITY: package count, e.g. 1, 20, 40.",
            "- PACKAGE_UNIT: package unit, e.g. frasco, comprimidos, ampollas.",
            "- REGULATORY_MARKER: EFG, DH, ECM, TLD.",
            "",
            "Example input:",
            "{\"tokens\":[\"ibuprofeno\",\"cinfa\",\"600\",\"mg\",\"comprimidos\",\"recubiertos\",\"con\",\"pelicula\",\"efg\",\"40\",\"comprimidos\"],\"tokenCount\":11}",
            "",
            "Example output:",
            "{\"labels\":[\"B-ACTIVE_INGREDIENT\",\"O\",\"B-STRENGTH\",\"I-STRENGTH\",\"B-DOSE_FORM\",\"I-DOSE_FORM\",\"I-DOSE_FORM\",\"I-DOSE_FORM\",\"B-REGULATORY_MARKER\",\"B-PACKAGE_QUANTITY\",\"B-PACKAGE_UNIT\"]}");
    }

    public string BuildUserPrompt(PharmaAnnotationModelRequest request)
    {
        var payload = new PharmaAnnotationPromptPayload
        {
            Tokens = request.Tokens.Select(t => t.Text).ToArray(),
            TokenCount = request.Tokens.Count,
        };

        return JsonSerializer.Serialize(payload, SerOpts);
    }

    private static string BuildRepairPrompt(int tokenCount, IReadOnlyList<string> previousErrors)
    {
        return string.Join('\n',
            "Your previous answer was invalid.",
            "",
            "Errors:",
            string.Join('\n', previousErrors.Select(e => $"- {e}")),
            "",
            "Return only corrected JSON:",
            "{\"labels\":[...]}",
            "",
            $"labels.length must equal {tokenCount}.");
    }
}
