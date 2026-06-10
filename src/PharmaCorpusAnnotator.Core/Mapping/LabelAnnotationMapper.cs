using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Mapping;

public sealed class LabelAnnotationMapper
{
    public PharmaAnnotationResponse Map(
        PharmaAnnotationModelRequest request,
        PharmaLabelAnnotationResponse labelResponse)
    {
        var tokens = request.Tokens
            .Select((token, i) => new AnnotatedToken(
                token.Index,
                token.Text,
                labelResponse.Labels[i],
                Normalized: null,
                Confidence: null))
            .ToList();

        var normalized = new NormalizedPharmaItem(
            ProductName: null,
            Brand: null,
            Manufacturer: null,
            ActiveIngredients: GetEntityTexts(request, labelResponse, LabelSchema.ActiveIngredient),
            Strength: JoinEntityTexts(request, labelResponse, LabelSchema.Strength),
            DoseForm: JoinEntityTexts(request, labelResponse, LabelSchema.DoseForm),
            Route: JoinEntityTexts(request, labelResponse, LabelSchema.Route),
            PackageQuantity: ParsePackageQuantity(request, labelResponse),
            PackageUnit: JoinEntityTexts(request, labelResponse, LabelSchema.PackageUnit),
            PackageVolume: JoinEntityTexts(request, labelResponse, LabelSchema.PackageVolume),
            Price: null,
            Currency: null);

        var quality = new AnnotationQuality(
            Confidence: null,
            NeedsReview: false,
            Warnings: []);

        return new PharmaAnnotationResponse(tokens, normalized, quality);
    }

    private static int? ParsePackageQuantity(
        PharmaAnnotationModelRequest request,
        PharmaLabelAnnotationResponse response)
    {
        var value = JoinEntityTexts(request, response, LabelSchema.PackageQuantity);
        return int.TryParse(value, out var quantity) ? quantity : null;
    }

    private static string? JoinEntityTexts(
        PharmaAnnotationModelRequest request,
        PharmaLabelAnnotationResponse response,
        string entityType)
    {
        var values = GetEntityTexts(request, response, entityType);
        return values.Count == 0 ? null : string.Join("; ", values);
    }

    private static IReadOnlyList<string> GetEntityTexts(
        PharmaAnnotationModelRequest request,
        PharmaLabelAnnotationResponse response,
        string entityType)
    {
        var values = new List<string>();
        var current = new List<string>();
        var begin = "B-" + entityType;
        var inside = "I-" + entityType;

        for (int i = 0; i < response.Labels.Count; i++)
        {
            var label = response.Labels[i];

            if (label == begin)
            {
                Flush();
                current.Add(request.Tokens[i].Text);
            }
            else if (label == inside && current.Count > 0)
            {
                current.Add(request.Tokens[i].Text);
            }
            else
            {
                Flush();
            }
        }

        Flush();
        return values;

        void Flush()
        {
            if (current.Count == 0)
                return;

            values.Add(string.Join(" ", current));
            current.Clear();
        }
    }
}
