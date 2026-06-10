namespace PharmaCorpusAnnotator.Core.Models;

public sealed record PharmaEntitySpan(
    string Type,
    int Start,
    int End,
    double? Confidence);
