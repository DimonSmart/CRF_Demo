using PharmaCorpusAnnotator.Core.Interfaces;
using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Cli;

/// <summary>Used only in dry-run mode where no LLM is called.</summary>
internal sealed class NullModelClient : IPharmaAnnotationModelClient
{
    public Task<PharmaAnnotationResponse> AnnotateAsync(
        PharmaAnnotationModelRequest request,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("LLM not configured in dry-run mode.");
}
