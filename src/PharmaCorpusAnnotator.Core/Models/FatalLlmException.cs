namespace PharmaCorpusAnnotator.Core.Models;

public sealed class FatalLlmException : Exception
{
    public FatalLlmException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
