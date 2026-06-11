namespace CrfDemo.Features;

public sealed record TokenFeatures(string Token, IReadOnlyList<string> Values);
