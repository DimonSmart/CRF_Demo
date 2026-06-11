using CrfDemo.Corpus;

namespace CrfDemo.Training;

public static class TrainingDataBuilder
{
    public static IReadOnlyList<TrainingSequence> Build(CorpusDocument corpus)
    {
        return CorpusSequences.Records(corpus)
            .Where(r => !string.IsNullOrWhiteSpace(r.Text))
            .Select(r => new TrainingSequence(
                r.Text!,
                r.Annotation?.Tokens.Select(t => t.Text).ToArray() ?? Array.Empty<string>(),
                r.Annotation?.Tokens.Select(t => t.Label).ToArray() ?? Array.Empty<string>()))
            .Where(s => s.Tokens.Count > 0 && s.Tokens.Count == s.Labels.Count)
            .ToArray();
    }
}
