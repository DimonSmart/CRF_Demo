using System.Text.Json;
using System.Text.Json.Serialization;
using CrfDemo.Features;

namespace CrfDemo.Inference;

public sealed class TrainedSequenceLabeler : ISequenceLabeler
{
    private const string Start = "<START>";
    private readonly Dictionary<string, double> _emissionWeights;
    private readonly Dictionary<string, double> _transitionWeights;

    [JsonConstructor]
    public TrainedSequenceLabeler(
        IReadOnlyList<string> labels,
        Dictionary<string, double> emissionWeights,
        Dictionary<string, double> transitionWeights)
    {
        Labels = labels;
        _emissionWeights = emissionWeights;
        _transitionWeights = transitionWeights;
    }

    public IReadOnlyList<string> Labels { get; }
    public Dictionary<string, double> EmissionWeights => _emissionWeights;
    public Dictionary<string, double> TransitionWeights => _transitionWeights;

    public IReadOnlyList<string> Predict(IReadOnlyList<TokenFeatures> tokens)
    {
        if (tokens.Count == 0)
            return Array.Empty<string>();

        var labelCount = Labels.Count;
        var scores = new double[tokens.Count, labelCount];
        var back = new int[tokens.Count, labelCount];

        for (var label = 0; label < labelCount; label++)
        {
            scores[0, label] = TransitionScore(Start, Labels[label]) + EmissionScore(Labels[label], tokens[0]);
            back[0, label] = -1;
        }

        for (var t = 1; t < tokens.Count; t++)
        {
            for (var label = 0; label < labelCount; label++)
            {
                var bestScore = double.NegativeInfinity;
                var bestPrevious = 0;
                for (var previous = 0; previous < labelCount; previous++)
                {
                    var score = scores[t - 1, previous] + TransitionScore(Labels[previous], Labels[label]);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPrevious = previous;
                    }
                }

                scores[t, label] = bestScore + EmissionScore(Labels[label], tokens[t]);
                back[t, label] = bestPrevious;
            }
        }

        var best = 0;
        var finalScore = double.NegativeInfinity;
        for (var label = 0; label < labelCount; label++)
        {
            if (scores[tokens.Count - 1, label] > finalScore)
            {
                finalScore = scores[tokens.Count - 1, label];
                best = label;
            }
        }

        var result = new string[tokens.Count];
        for (var t = tokens.Count - 1; t >= 0; t--)
        {
            result[t] = Labels[best];
            best = back[t, best] < 0 ? 0 : back[t, best];
        }

        return result;
    }

    public double EmissionScore(string label, TokenFeatures features)
    {
        var sum = 0.0;
        foreach (var feature in features.Values)
            sum += _emissionWeights.GetValueOrDefault(EmissionKey(label, feature));
        return sum;
    }

    public double TransitionScore(string previous, string label)
    {
        return _transitionWeights.GetValueOrDefault(TransitionKey(previous, label));
    }

    public static string EmissionKey(string label, string feature) => $"{label}\u001f{feature}";
    public static string TransitionKey(string previous, string label) => $"{previous}\u001f{label}";

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static TrainedSequenceLabeler Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TrainedSequenceLabeler>(json)
            ?? throw new InvalidOperationException("Model file is empty or invalid JSON.");
    }
}
