namespace PharmaCorpusAnnotator.Core.Llm;

internal sealed class PharmaAnnotationPromptPayload
{
    public required string[] Tokens { get; init; }
    public required int TokenCount { get; init; }
}
