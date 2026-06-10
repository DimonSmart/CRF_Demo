using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Mapping;

public sealed class SpanAnnotationMapper
{
    public PharmaAnnotationResponse Map(
        PharmaAnnotationModelRequest request,
        PharmaSpanAnnotationResponse spanResponse)
    {
        var spansByStart = spanResponse.Spans
            .OrderBy(s => s.Start)
            .ToDictionary(s => s.Start);

        var tokens = new List<AnnotatedToken>(request.Tokens.Count);
        PharmaEntitySpan? activeSpan = null;

        foreach (var sourceToken in request.Tokens)
        {
            if (spansByStart.TryGetValue(sourceToken.Index, out var nextSpan))
                activeSpan = nextSpan;

            var label = "O";
            double? confidence = null;

            if (activeSpan is not null &&
                sourceToken.Index >= activeSpan.Start &&
                sourceToken.Index <= activeSpan.End)
            {
                label = sourceToken.Index == activeSpan.Start
                    ? $"B-{activeSpan.Type}"
                    : $"I-{activeSpan.Type}";
                confidence = activeSpan.Confidence;
            }

            tokens.Add(new AnnotatedToken(
                sourceToken.Index,
                sourceToken.Text,
                label,
                null,
                confidence));

            if (activeSpan is not null && sourceToken.Index >= activeSpan.End)
                activeSpan = null;
        }

        var normalized = new NormalizedPharmaItem(
            ProductName: null,
            Brand: null,
            Manufacturer: null,
            ActiveIngredients: GetSpanTexts(request, spanResponse, "ACTIVE_INGREDIENT"),
            Strength: JoinSpanTexts(request, spanResponse, "STRENGTH"),
            DoseForm: JoinSpanTexts(request, spanResponse, "DOSE_FORM"),
            Route: JoinSpanTexts(request, spanResponse, "ROUTE"),
            PackageQuantity: null,
            PackageUnit: null,
            PackageVolume: null,
            Price: null,
            Currency: null);

        var confidences = spanResponse.Spans
            .Select(s => s.Confidence)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .ToList();

        var quality = new AnnotationQuality(
            confidences.Count == 0 ? null : confidences.Average(),
            spanResponse.NeedsReview,
            spanResponse.Warnings);

        return new PharmaAnnotationResponse(tokens, normalized, quality);
    }

    private static IReadOnlyList<string> GetSpanTexts(
        PharmaAnnotationModelRequest request,
        PharmaSpanAnnotationResponse spanResponse,
        string type) =>
        spanResponse.Spans
            .Where(s => s.Type == type)
            .Select(s => TextForSpan(request, s))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

    private static string? JoinSpanTexts(
        PharmaAnnotationModelRequest request,
        PharmaSpanAnnotationResponse spanResponse,
        string type)
    {
        var values = GetSpanTexts(request, spanResponse, type);
        return values.Count == 0 ? null : string.Join("; ", values);
    }

    private static string TextForSpan(PharmaAnnotationModelRequest request, PharmaEntitySpan span) =>
        string.Join(" ", request.Tokens
            .Where(t => t.Index >= span.Start && t.Index <= span.End)
            .Select(t => t.Text));
}
