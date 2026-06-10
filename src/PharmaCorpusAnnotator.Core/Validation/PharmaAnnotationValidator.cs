using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Validation;

public sealed class PharmaAnnotationValidator
{
    public AgenticModelValidationResult Validate(
        PharmaAnnotationModelRequest request,
        PharmaAnnotationResponse? response)
    {
        var errors = new List<string>();

        if (response is null)
        {
            errors.Add("response is null.");
            return AgenticModelValidationResult.Failure(errors.ToArray());
        }

        if (response.Tokens is null)
        {
            errors.Add("response.tokens is null.");
            return AgenticModelValidationResult.Failure(errors.ToArray());
        }

        if (response.Tokens.Count != request.Tokens.Count)
        {
            errors.Add(
                $"response contains {response.Tokens.Count} tokens, input contains {request.Tokens.Count} tokens.");
        }

        var allowedSet = new HashSet<string>(request.AllowedLabels, StringComparer.Ordinal);
        string? previousLabel = null;

        for (int i = 0; i < Math.Min(response.Tokens.Count, request.Tokens.Count); i++)
        {
            var rt = response.Tokens[i];
            var it = request.Tokens[i];

            if (rt.Index != it.Index)
                errors.Add($"tokens[{i}].index {rt.Index} does not match input token index {it.Index}.");

            if (rt.Text != it.Text)
                errors.Add($"tokens[{i}].text '{rt.Text}' does not match input token text '{it.Text}'.");

            if (!allowedSet.Contains(rt.Label))
                errors.Add($"tokens[{i}].label has unsupported value {rt.Label}.");

            if (rt.Confidence.HasValue && (rt.Confidence < 0 || rt.Confidence > 1))
                errors.Add($"tokens[{i}].confidence {rt.Confidence} is out of range [0,1].");

            if (rt.Label.StartsWith("I-", StringComparison.Ordinal))
            {
                string iType = rt.Label[2..];

                if (i == 0)
                {
                    errors.Add($"tokens[{i}] uses {rt.Label} as first token.");
                }
                else if (previousLabel is null ||
                         (!previousLabel.Equals("B-" + iType, StringComparison.Ordinal) &&
                          !previousLabel.Equals("I-" + iType, StringComparison.Ordinal)))
                {
                    errors.Add(
                        $"tokens[{i}] uses {rt.Label} after {previousLabel ?? "null"} (BIO sequence violation).");
                }
            }

            previousLabel = rt.Label;
        }

        if (response.Normalized is null)
        {
            errors.Add("response.normalized is null.");
        }
        else
        {
            if (response.Normalized.ActiveIngredients is null)
                errors.Add("response.normalized.activeIngredients is null.");
        }

        if (response.Quality is null)
        {
            errors.Add("response.quality is null.");
        }
        else
        {
            if (response.Quality.Warnings is null)
                errors.Add("response.quality.warnings is null.");

            if (response.Quality.Confidence.HasValue &&
                (response.Quality.Confidence < 0 || response.Quality.Confidence > 1))
            {
                errors.Add(
                    $"response.quality.confidence {response.Quality.Confidence} is out of range [0,1].");
            }
        }

        return errors.Count == 0
            ? AgenticModelValidationResult.Success()
            : AgenticModelValidationResult.Failure(errors.ToArray());
    }
}
