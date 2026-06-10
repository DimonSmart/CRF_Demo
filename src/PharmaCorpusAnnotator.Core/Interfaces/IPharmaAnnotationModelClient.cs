using PharmaCorpusAnnotator.Core.Models;

namespace PharmaCorpusAnnotator.Core.Interfaces;

public interface IPharmaAnnotationModelClient
{
    Task<PharmaAnnotationResponse> AnnotateAsync(
        PharmaAnnotationModelRequest request,
        CancellationToken cancellationToken = default);
}
