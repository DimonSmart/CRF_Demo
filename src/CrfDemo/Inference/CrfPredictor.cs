using CrfDemo.Features;
using CrfDemo.Tokenization;

namespace CrfDemo.Inference;

public sealed class CrfPredictor
{
    private readonly TrainedSequenceLabeler _model;
    private readonly PharmaTokenizer _tokenizer = new();
    private readonly TokenFeatureExtractor _featureExtractor = new();

    public CrfPredictor(TrainedSequenceLabeler model)
    {
        _model = model;
    }

    public IReadOnlyList<TaggedToken> Predict(string text)
    {
        var tokenized = _tokenizer.Tokenize(text);
        var features = _featureExtractor.Extract(tokenized.Tokens);
        var labels = _model.Predict(features);

        return tokenized.Tokens
            .Select((token, index) => new TaggedToken(token.Index, token.Text, labels[index]))
            .ToArray();
    }
}
