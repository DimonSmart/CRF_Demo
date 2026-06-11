namespace CrfDemo.Corpus;

public sealed class CorpusStatistics
{
    public int SourceCount { get; init; }
    public int RecordCount { get; init; }
    public int TokenCount { get; init; }
    public int LabelCount { get; init; }
    public Dictionary<string, int> LabelDistribution { get; init; } = new(StringComparer.Ordinal);
    public int MinTokens { get; init; }
    public double AverageTokens { get; init; }
    public int MaxTokens { get; init; }
    public int RecordsWithWarnings { get; init; }
}

public static class CorpusStatisticsBuilder
{
    public static CorpusStatistics Build(CorpusDocument corpus)
    {
        var records = CorpusSequences.Records(corpus).ToArray();
        var lengths = records.Select(r => r.Annotation?.Tokens.Count ?? 0).Where(x => x > 0).ToArray();
        var distribution = new Dictionary<string, int>(StringComparer.Ordinal);
        var tokenCount = 0;
        var warningRecords = 0;

        foreach (var record in records)
        {
            var tokens = record.Annotation?.Tokens ?? Array.Empty<CorpusToken>();
            tokenCount += tokens.Count;
            foreach (var token in tokens)
                distribution[token.Label] = distribution.GetValueOrDefault(token.Label) + 1;

            if ((record.Annotation?.Quality?.Warnings.Count ?? 0) > 0)
                warningRecords++;
        }

        return new CorpusStatistics
        {
            SourceCount = corpus.Sources.Count,
            RecordCount = records.Length,
            TokenCount = tokenCount,
            LabelCount = corpus.AnnotationSchema?.Labels.Count ?? 0,
            LabelDistribution = distribution,
            MinTokens = lengths.Length == 0 ? 0 : lengths.Min(),
            AverageTokens = lengths.Length == 0 ? 0 : lengths.Average(),
            MaxTokens = lengths.Length == 0 ? 0 : lengths.Max(),
            RecordsWithWarnings = warningRecords
        };
    }
}

public static class CorpusSequences
{
    public static IEnumerable<CorpusRecord> Records(CorpusDocument corpus)
    {
        foreach (var source in corpus.Sources)
        foreach (var record in source.Records)
            yield return record;
    }
}
