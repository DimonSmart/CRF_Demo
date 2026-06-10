namespace PharmaCorpusAnnotator.Core.Models;

public sealed record SourceToken(
    int Index,
    string Text,
    int StartOffset,
    int EndOffset);
