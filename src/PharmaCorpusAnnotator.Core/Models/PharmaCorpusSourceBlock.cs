namespace PharmaCorpusAnnotator.Core.Models;

public sealed record PharmaCorpusSourceBlock(
    PharmaCorpusSourceHeader Source,
    IReadOnlyList<PharmaCorpusRecord> Records);
