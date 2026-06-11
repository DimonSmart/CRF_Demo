using CrfDemo.Features;
using CrfDemo.Inference;

namespace CrfDemo.Training;

public sealed class CrfTrainer : ISequenceLabelerTrainer
{
    private const string Start = "<START>";
    private readonly TrainingOptions _options;
    private readonly TokenFeatureExtractor _featureExtractor = new();

    public CrfTrainer(TrainingOptions options)
    {
        _options = options;
    }

    public TrainedSequenceLabeler Train(IReadOnlyList<TrainingSequence> sequences, IReadOnlyList<string> labels)
    {
        var model = CreateModel(labels);

        var random = new Random(_options.Seed);
        var train = sequences.OrderBy(_ => random.Next()).ToArray();

        for (var epoch = 0; epoch < _options.Epochs; epoch++)
            TrainEpoch(model, train);

        return model;
    }

    public TrainedSequenceLabeler CreateModel(IReadOnlyList<string> labels)
    {
        return new TrainedSequenceLabeler(
            labels.ToArray(),
            new Dictionary<string, double>(StringComparer.Ordinal),
            new Dictionary<string, double>(StringComparer.Ordinal));
    }

    public IReadOnlyList<TrainingSequence> Shuffle(IReadOnlyList<TrainingSequence> sequences)
    {
        var random = new Random(_options.Seed);
        return sequences.OrderBy(_ => random.Next()).ToArray();
    }

    public void TrainEpoch(TrainedSequenceLabeler model, IReadOnlyList<TrainingSequence> sequences)
    {
        foreach (var sequence in sequences)
            Update(model, sequence);
    }

    private void Update(TrainedSequenceLabeler model, TrainingSequence sequence)
    {
        var features = _featureExtractor.Extract(sequence.Tokens);
        var labels = model.Labels;
        var emissionScores = BuildEmissionScores(model, features);
        var forward = Forward(model, emissionScores);
        var backward = Backward(model, emissionScores);
        var logZ = LogSumExp(forward, features.Count - 1, labels.Count);

        for (var t = 0; t < features.Count; t++)
        {
            var goldLabel = sequence.Labels[t];
            AddEmission(model, goldLabel, features[t], _options.LearningRate);

            for (var label = 0; label < labels.Count; label++)
            {
                var probability = Math.Exp(forward[t, label] + backward[t, label] - logZ);
                AddEmission(model, labels[label], features[t], -_options.LearningRate * probability);
            }
        }

        for (var t = 0; t < features.Count; t++)
        {
            var goldPrevious = t == 0 ? Start : sequence.Labels[t - 1];
            var goldCurrent = sequence.Labels[t];
            AddTransition(model, goldPrevious, goldCurrent, _options.LearningRate);

            if (t == 0)
            {
                for (var current = 0; current < labels.Count; current++)
                {
                    var score = model.TransitionScore(Start, labels[current]) + emissionScores[t, current] + backward[t, current] - logZ;
                    AddTransition(model, Start, labels[current], -_options.LearningRate * Math.Exp(score));
                }
            }
            else
            {
                for (var previous = 0; previous < labels.Count; previous++)
                for (var current = 0; current < labels.Count; current++)
                {
                    var score = forward[t - 1, previous]
                                + model.TransitionScore(labels[previous], labels[current])
                                + emissionScores[t, current]
                                + backward[t, current]
                                - logZ;
                    AddTransition(model, labels[previous], labels[current], -_options.LearningRate * Math.Exp(score));
                }
            }
        }

        ApplyL2(model.EmissionWeights);
        ApplyL2(model.TransitionWeights);
    }

    private double[,] BuildEmissionScores(TrainedSequenceLabeler model, IReadOnlyList<TokenFeatures> features)
    {
        var scores = new double[features.Count, model.Labels.Count];
        for (var t = 0; t < features.Count; t++)
        for (var label = 0; label < model.Labels.Count; label++)
            scores[t, label] = model.EmissionScore(model.Labels[label], features[t]);
        return scores;
    }

    private static double[,] Forward(TrainedSequenceLabeler model, double[,] emissionScores)
    {
        var count = emissionScores.GetLength(0);
        var labelCount = model.Labels.Count;
        var forward = new double[count, labelCount];

        for (var label = 0; label < labelCount; label++)
            forward[0, label] = model.TransitionScore(Start, model.Labels[label]) + emissionScores[0, label];

        for (var t = 1; t < count; t++)
        for (var label = 0; label < labelCount; label++)
        {
            var current = label;
            var max = double.NegativeInfinity;
            for (var previous = 0; previous < labelCount; previous++)
            {
                var value = forward[t - 1, previous]
                            + model.TransitionScore(model.Labels[previous], model.Labels[current]);
                if (value > max)
                    max = value;
            }

            var sum = 0.0;
            for (var previous = 0; previous < labelCount; previous++)
            {
                var value = forward[t - 1, previous]
                            + model.TransitionScore(model.Labels[previous], model.Labels[current]);
                sum += Math.Exp(value - max);
            }

            forward[t, label] = max + Math.Log(sum) + emissionScores[t, label];
        }

        return forward;
    }

    private static double[,] Backward(TrainedSequenceLabeler model, double[,] emissionScores)
    {
        var count = emissionScores.GetLength(0);
        var labelCount = model.Labels.Count;
        var backward = new double[count, labelCount];

        for (var t = count - 2; t >= 0; t--)
        for (var label = 0; label < labelCount; label++)
        {
            var previous = label;
            var max = double.NegativeInfinity;
            for (var current = 0; current < labelCount; current++)
            {
                var value = model.TransitionScore(model.Labels[previous], model.Labels[current])
                            + emissionScores[t + 1, current]
                            + backward[t + 1, current];
                if (value > max)
                    max = value;
            }

            var sum = 0.0;
            for (var current = 0; current < labelCount; current++)
            {
                var value = model.TransitionScore(model.Labels[previous], model.Labels[current])
                            + emissionScores[t + 1, current]
                            + backward[t + 1, current];
                sum += Math.Exp(value - max);
            }

            backward[t, label] = max + Math.Log(sum);
        }

        return backward;
    }

    private void AddEmission(TrainedSequenceLabeler model, string label, TokenFeatures features, double amount)
    {
        foreach (var feature in features.Values)
        {
            var key = TrainedSequenceLabeler.EmissionKey(label, feature);
            model.EmissionWeights[key] = model.EmissionWeights.GetValueOrDefault(key) + amount;
        }
    }

    private static void AddTransition(TrainedSequenceLabeler model, string previous, string current, double amount)
    {
        var key = TrainedSequenceLabeler.TransitionKey(previous, current);
        model.TransitionWeights[key] = model.TransitionWeights.GetValueOrDefault(key) + amount;
    }

    private void ApplyL2(Dictionary<string, double> weights)
    {
        if (_options.L2 <= 0)
            return;

        var factor = 1 - _options.LearningRate * _options.L2;
        foreach (var key in weights.Keys.ToArray())
            weights[key] *= factor;
    }

    private static double LogSumExp(double[,] values, int row, int count)
    {
        var max = double.NegativeInfinity;
        for (var i = 0; i < count; i++)
            if (values[row, i] > max)
                max = values[row, i];

        if (double.IsNegativeInfinity(max))
            return max;

        var sum = 0.0;
        for (var i = 0; i < count; i++)
            sum += Math.Exp(values[row, i] - max);

        return max + Math.Log(sum);
    }

}
