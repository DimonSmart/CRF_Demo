using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Validation;

public sealed class PharmaLabelAnnotationValidator
{
    public AgenticModelValidationResult Validate(
        PharmaAnnotationModelRequest request,
        PharmaLabelAnnotationResponse? response)
    {
        var errors = new List<string>();

        if (response is null)
        {
            errors.Add("response is null.");
            return AgenticModelValidationResult.Failure(errors.ToArray());
        }

        if (response.Labels is null)
        {
            errors.Add("response.labels is null.");
            return AgenticModelValidationResult.Failure(errors.ToArray());
        }

        if (response.Labels.Count != request.Tokens.Count)
        {
            errors.Add(
                $"Label count mismatch: expected {request.Tokens.Count} labels, got {response.Labels.Count}.");
        }

        var allowedLabels = new HashSet<string>(LabelSchema.AllLabels, StringComparer.Ordinal);
        string? previousLabel = null;

        for (int i = 0; i < response.Labels.Count; i++)
        {
            var label = response.Labels[i];

            if (!allowedLabels.Contains(label))
            {
                errors.Add($"Unknown label at token {i}: {label}.");
                previousLabel = label;
                continue;
            }

            if (label.StartsWith("I-", StringComparison.Ordinal))
            {
                var entityType = label[2..];
                var expectedBegin = "B-" + entityType;
                var expectedInside = "I-" + entityType;

                if (previousLabel != expectedBegin && previousLabel != expectedInside)
                {
                    errors.Add(
                        $"Invalid BIO transition at token {i}: {label} cannot follow {previousLabel ?? "START"}.");
                }
            }

            previousLabel = label;
        }

        return errors.Count == 0
            ? AgenticModelValidationResult.Success()
            : AgenticModelValidationResult.Failure(errors.ToArray());
    }
}
