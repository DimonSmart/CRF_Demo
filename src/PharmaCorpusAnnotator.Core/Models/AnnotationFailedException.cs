namespace PharmaCorpusAnnotator.Core.Models;

public sealed class AnnotationFailedException : Exception
{
    public string SourceKey { get; }
    public long RowNumber { get; }
    public string Text { get; }
    public string ValidationError { get; }
    public int Attempts { get; }

    public AnnotationFailedException(
        string sourceKey,
        long rowNumber,
        string text,
        string validationError,
        int attempts)
        : base($"Annotation failed after {attempts} attempt(s) for {sourceKey}:{rowNumber}. Error: {validationError}")
    {
        SourceKey = sourceKey;
        RowNumber = rowNumber;
        Text = text;
        ValidationError = validationError;
        Attempts = attempts;
    }
}
