using CrfDemo.Corpus;
using CrfDemo.Features;
using CrfDemo.Inference;

namespace CrfDemo.Training;

public sealed class CrfTrainingWorkflow
{
    private readonly TrainingOptions _options;
    private readonly TokenFeatureExtractor _featureExtractor = new();

    public CrfTrainingWorkflow(TrainingOptions options)
    {
        _options = options;
    }

    public TrainingReport Train(CorpusDocument corpus, string modelPath)
    {
        var sequences = TrainingDataBuilder.Build(corpus);
        var labels = corpus.AnnotationSchema?.Labels ?? Array.Empty<string>();
        var split = Split(sequences);
        var trainer = new CrfTrainer(_options);
        var model = trainer.Train(split.Train, labels);
        model.Save(modelPath);

        var evaluation = Evaluate(model, split.Validation, sequences.Count);
        return new TrainingReport
        {
            TrainingRecords = split.Train.Count,
            ValidationRecords = split.Validation.Count,
            LabelCount = labels.Count,
            TokenCount = split.Train.Sum(s => s.Tokens.Count),
            ModelPath = modelPath,
            Evaluation = evaluation
        };
    }

    public EvaluationReport Evaluate(TrainedSequenceLabeler model, IReadOnlyList<TrainingSequence> validation, int totalRows)
    {
        var metrics = model.Labels.ToDictionary(label => label, _ => new MutableLabelMetrics(), StringComparer.Ordinal);
        var correct = 0;
        var tokenCount = 0;
        var errors = new List<PredictionError>();

        foreach (var sequence in validation)
        {
            var features = _featureExtractor.Extract(sequence.Tokens);
            var predicted = model.Predict(features);
            var rowErrors = new List<TokenPrediction>();

            for (var i = 0; i < predicted.Count; i++)
            {
                tokenCount++;
                var expected = sequence.Labels[i];
                var actual = predicted[i];
                if (expected == actual)
                {
                    correct++;
                    metrics[expected].TruePositive++;
                }
                else
                {
                    metrics[expected].FalseNegative++;
                    if (metrics.TryGetValue(actual, out var actualMetrics))
                        actualMetrics.FalsePositive++;

                    rowErrors.Add(new TokenPrediction(sequence.Tokens[i], expected, actual));
                }
            }

            if (rowErrors.Count > 0 && errors.Count < 5)
                errors.Add(new PredictionError(sequence.Text, rowErrors));
        }

        var immutable = metrics.ToDictionary(
            x => x.Key,
            x => new LabelMetrics(x.Value.TruePositive, x.Value.FalsePositive, x.Value.FalseNegative),
            StringComparer.Ordinal);

        var microTp = immutable.Values.Sum(x => x.TruePositive);
        var microFp = immutable.Values.Sum(x => x.FalsePositive);
        var microFn = immutable.Values.Sum(x => x.FalseNegative);
        var micro = new LabelMetrics(microTp, microFp, microFn);

        return new EvaluationReport
        {
            TotalRows = totalRows,
            ValidationRows = validation.Count,
            TokenCount = tokenCount,
            Accuracy = tokenCount == 0 ? 0 : (double)correct / tokenCount,
            MicroF1 = micro.F1,
            MacroF1 = immutable.Count == 0 ? 0 : immutable.Values.Average(x => x.F1),
            Labels = immutable,
            Errors = errors
        };
    }

    public (IReadOnlyList<TrainingSequence> Train, IReadOnlyList<TrainingSequence> Validation) Split(IReadOnlyList<TrainingSequence> sequences)
    {
        var random = new Random(_options.Seed);
        var shuffled = sequences.OrderBy(_ => random.Next()).ToArray();
        var validationCount = Math.Max(1, (int)Math.Round(shuffled.Length * _options.ValidationShare));
        validationCount = Math.Min(validationCount, Math.Max(0, shuffled.Length - 1));
        return (shuffled.Skip(validationCount).ToArray(), shuffled.Take(validationCount).ToArray());
    }

    private sealed class MutableLabelMetrics
    {
        public int TruePositive { get; set; }
        public int FalsePositive { get; set; }
        public int FalseNegative { get; set; }
    }
}
