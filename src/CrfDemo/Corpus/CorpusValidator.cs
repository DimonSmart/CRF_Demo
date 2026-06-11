namespace CrfDemo.Corpus;

public sealed class CorpusValidationReport
{
    public List<string> CriticalErrors { get; } = [];
    public List<string> Warnings { get; } = [];
    public int BioErrors { get; set; }
    public int UnknownLabelErrors { get; set; }
    public bool IsValidForTraining => CriticalErrors.Count == 0;
}

public static class CorpusValidator
{
    public static CorpusValidationReport Validate(CorpusDocument corpus)
    {
        var report = new CorpusValidationReport();

        if (string.IsNullOrWhiteSpace(corpus.SchemaVersion))
            report.CriticalErrors.Add("schemaVersion is missing.");

        var labels = corpus.AnnotationSchema?.Labels ?? Array.Empty<string>();
        if (labels.Count == 0)
            report.CriticalErrors.Add("annotationSchema.labels is empty.");

        var labelSet = labels.ToHashSet(StringComparer.Ordinal);
        foreach (var record in CorpusSequences.Records(corpus))
        {
            if (string.IsNullOrWhiteSpace(record.Text))
            {
                report.CriticalErrors.Add($"Record {record.RowNumber}: text is missing.");
                continue;
            }

            var tokens = record.Annotation?.Tokens ?? Array.Empty<CorpusToken>();
            if (tokens.Count == 0)
            {
                report.CriticalErrors.Add($"Record {record.RowNumber}: annotation.tokens is empty.");
                continue;
            }

            for (var i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Index != i)
                    report.CriticalErrors.Add($"Record {record.RowNumber}: token index {tokens[i].Index} must be {i}.");

                if (!labelSet.Contains(tokens[i].Label))
                {
                    report.UnknownLabelErrors++;
                    report.CriticalErrors.Add($"Record {record.RowNumber}, token {i}: unknown label '{tokens[i].Label}'.");
                }
            }

            report.BioErrors += CountBioErrors(tokens);

            var warnings = record.Annotation?.Quality?.Warnings ?? Array.Empty<string>();
            if (warnings.Count > 0)
                report.Warnings.Add($"Record {record.RowNumber}: {string.Join("; ", warnings)}");
        }

        if (report.BioErrors > 0)
            report.CriticalErrors.Add($"BIO transition errors: {report.BioErrors}.");

        return report;
    }

    private static int CountBioErrors(IReadOnlyList<CorpusToken> tokens)
    {
        var errors = 0;
        string? previousEntity = null;

        foreach (var token in tokens)
        {
            var label = token.Label;
            if (label == "O")
            {
                previousEntity = null;
                continue;
            }

            var entity = LabelEntity(label);
            if (label.StartsWith("B-", StringComparison.Ordinal))
            {
                previousEntity = entity;
                continue;
            }

            if (label.StartsWith("I-", StringComparison.Ordinal))
            {
                if (previousEntity != entity)
                    errors++;
                previousEntity = entity;
            }
        }

        return errors;
    }

    private static string? LabelEntity(string label)
    {
        var index = label.IndexOf('-', StringComparison.Ordinal);
        return index < 0 ? null : label[(index + 1)..];
    }
}
