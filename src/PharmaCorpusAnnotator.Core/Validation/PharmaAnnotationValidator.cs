using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Validation;

public sealed class PharmaAnnotationValidator
{
    private static readonly HashSet<string> PriceContextColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Precio de venta al público con IVA",
        "Precio de referencia",
    };

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

            // BIO validation
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

            // Cross-entity BIO violation: I-STRENGTH after B-PRODUCT_NAME
            if (rt.Label == "I-STRENGTH" && previousLabel == "B-PRODUCT_NAME")
                errors.Add($"tokens[{i}] uses I-STRENGTH after B-PRODUCT_NAME.");

            // I-DOSE_FORM after B-STRENGTH is invalid
            if (rt.Label == "I-DOSE_FORM" && previousLabel == "B-STRENGTH")
                errors.Add($"tokens[{i}] uses I-DOSE_FORM after B-STRENGTH.");

            previousLabel = rt.Label;
        }

        // Normalized validation
        if (response.Normalized is null)
        {
            errors.Add("response.normalized is null.");
        }
        else
        {
            if (response.Normalized.ActiveIngredients is null)
                errors.Add("response.normalized.activeIngredients is null.");

            // Price hallucination check
            if (response.Normalized.Price.HasValue)
            {
                bool priceInText = response.Tokens
                    .Any(t => t.Label == "B-PRICE" || t.Label == "I-PRICE");
                bool priceInContext = request.Context.Keys
                    .Any(k => PriceContextColumns.Contains(k) &&
                              !string.IsNullOrWhiteSpace(request.Context[k]));

                if (!priceInText && !priceInContext)
                    errors.Add(
                        "normalized.price is set, but no price is present in source text or context.");
            }
        }

        // Quality validation
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

        // Active ingredient from context warning check
        if (response.Normalized?.ActiveIngredients?.Count > 0 && response.Tokens is not null)
        {
            bool hasActiveIngredientToken = response.Tokens
                .Any(t => t.Label == "B-ACTIVE_INGREDIENT" || t.Label == "I-ACTIVE_INGREDIENT");

            if (!hasActiveIngredientToken)
            {
                const string expectedWarning = "Active ingredient was taken from context, not from product text.";
                if (response.Quality?.Warnings?.All(w =>
                        !w.Contains("active ingredient", StringComparison.OrdinalIgnoreCase)) == true)
                {
                    errors.Add(
                        $"Active ingredient is set but no token is labeled ACTIVE_INGREDIENT and warning is missing: '{expectedWarning}'");
                }
            }
        }

        // Manufacturer from context warning check
        if (response.Normalized?.Manufacturer is not null && response.Tokens is not null)
        {
            bool hasManufacturerToken = response.Tokens
                .Any(t => t.Label == "B-MANUFACTURER" || t.Label == "I-MANUFACTURER");

            if (!hasManufacturerToken)
            {
                if (response.Quality?.Warnings?.All(w =>
                        !w.Contains("manufacturer", StringComparison.OrdinalIgnoreCase)) == true)
                {
                    errors.Add(
                        "Manufacturer is set but no token is labeled MANUFACTURER and warning is missing: 'Manufacturer was taken from context, not from product text.'");
                }
            }
        }

        return errors.Count == 0
            ? AgenticModelValidationResult.Success()
            : AgenticModelValidationResult.Failure(errors.ToArray());
    }
}
