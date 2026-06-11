using CrfDemo.Features;

namespace CrfDemo.Inference;

public interface ISequenceLabeler
{
    IReadOnlyList<string> Predict(IReadOnlyList<TokenFeatures> tokens);
}
