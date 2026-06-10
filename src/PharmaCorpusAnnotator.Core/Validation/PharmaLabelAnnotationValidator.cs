using PharmaCorpusAnnotator.Core.Labeling;
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
            errors.Add("response is null");
            return AgenticModelValidationResult.Failure(errors.ToArray());
        }

        if (response.Labels is null)
        {
            errors.Add("labels is null");
            return AgenticModelValidationResult.Failure(errors.ToArray());
        }

        if (response.Labels.Length != request.Tokens.Count)
        {
            errors.Add(
                $"labels count mismatch: expected {request.Tokens.Count}, actual {response.Labels.Length}");
        }

        string? previousLabel = null;

        for (int i = 0; i < response.Labels.Length; i++)
        {
            var label = response.Labels[i];

            if (!PharmaAnnotationLabels.AllSet.Contains(label))
            {
                errors.Add($"label at index {i} is not allowed: {label}");
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
                        $"invalid BIO transition at index {i}: {label} after {previousLabel ?? "START"}");
                }
            }

            previousLabel = label;
        }

        return errors.Count == 0
            ? AgenticModelValidationResult.Success()
            : AgenticModelValidationResult.Failure(errors.ToArray());
    }
}
