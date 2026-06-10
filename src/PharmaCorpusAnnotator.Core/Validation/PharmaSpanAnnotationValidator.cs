using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Validation;

public sealed class PharmaSpanAnnotationValidator
{
    public AgenticModelValidationResult Validate(
        PharmaAnnotationModelRequest request,
        PharmaSpanAnnotationResponse? response)
    {
        var errors = new List<string>();

        if (response is null)
        {
            errors.Add("response is null.");
            return AgenticModelValidationResult.Failure(errors.ToArray());
        }

        if (response.Spans is null)
        {
            errors.Add("response.spans is null.");
            return AgenticModelValidationResult.Failure(errors.ToArray());
        }

        if (response.Warnings is null)
            errors.Add("response.warnings is null.");

        var allowedTypes = new HashSet<string>(LabelSchema.SimpleEntityTypes, StringComparer.Ordinal);
        var ranges = new List<(int Start, int End)>();
        var lastTokenIndex = request.Tokens.Count - 1;

        for (int i = 0; i < response.Spans.Count; i++)
        {
            var span = response.Spans[i];

            if (span is null)
            {
                errors.Add($"spans[{i}] is null.");
                continue;
            }

            if (!allowedTypes.Contains(span.Type))
                errors.Add($"spans[{i}].type {span.Type} is not allowed in simple schema.");

            if (span.Start < 0)
                errors.Add($"spans[{i}].start {span.Start} is less than 0.");

            if (span.End < span.Start)
                errors.Add($"spans[{i}].end {span.End} is less than start {span.Start}.");

            if (span.End >= request.Tokens.Count)
                errors.Add($"spans[{i}].end {span.End} is outside token range 0..{lastTokenIndex}.");

            if (span.Confidence.HasValue && (span.Confidence < 0 || span.Confidence > 1))
                errors.Add($"spans[{i}].confidence {span.Confidence} is out of range [0,1].");

            if (span.Start >= 0 && span.End >= span.Start && span.End < request.Tokens.Count)
                ranges.Add((span.Start, span.End));
        }

        foreach (var range in ranges.OrderBy(r => r.Start).ThenBy(r => r.End).Pairwise())
        {
            if (range.Current.Start <= range.Previous.End)
            {
                errors.Add(
                    $"spans overlap: {range.Previous.Start}..{range.Previous.End} and {range.Current.Start}..{range.Current.End}.");
            }
        }

        return errors.Count == 0
            ? AgenticModelValidationResult.Success()
            : AgenticModelValidationResult.Failure(errors.ToArray());
    }
}

file static class SpanRangeExtensions
{
    public static IEnumerable<(T Previous, T Current)> Pairwise<T>(this IEnumerable<T> source)
    {
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext())
            yield break;

        var previous = enumerator.Current;
        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;
            yield return (previous, current);
            previous = current;
        }
    }
}
