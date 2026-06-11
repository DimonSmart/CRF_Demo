namespace CrfDemo.Training;

public sealed record TrainingSequence(string Text, IReadOnlyList<string> Tokens, IReadOnlyList<string> Labels);
