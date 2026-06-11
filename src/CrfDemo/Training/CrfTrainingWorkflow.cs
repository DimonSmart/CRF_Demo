using CrfDemo.Corpus;
using CrfDemo.Features;
using CrfDemo.Inference;
using System.Globalization;

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
        var train = trainer.Shuffle(split.Train);
        var model = trainer.CreateModel(labels);
        var epochReports = new List<EpochTrainingReport>();
        var validationDisabled = split.Validation.Count == 0;
        TrainedSequenceLabeler? bestModel = null;
        EvaluationReport? bestEvaluation = null;
        var bestMacroF1 = double.NegativeInfinity;
        int? bestEpoch = null;
        var epochsWithoutImprovement = 0;
        var earlyStoppingTriggered = false;
        var epochsCompleted = 0;

        for (var epoch = 1; epoch <= _options.Epochs; epoch++)
        {
            trainer.TrainEpoch(model, train);
            epochsCompleted = epoch;

            if (validationDisabled)
                continue;

            var evaluation = Evaluate(model, split.Validation, sequences.Count);
            var isBest = evaluation.MacroF1 > bestMacroF1 + 1e-9;
            if (isBest)
            {
                bestMacroF1 = evaluation.MacroF1;
                bestModel = model.Clone();
                bestEvaluation = evaluation;
                bestEpoch = epoch;
                epochsWithoutImprovement = 0;
            }
            else
            {
                epochsWithoutImprovement++;
            }

            var epochReport = new EpochTrainingReport
            {
                Epoch = epoch,
                EpochsRequested = _options.Epochs,
                TokenAccuracy = evaluation.Accuracy,
                MicroF1 = evaluation.MicroF1,
                MacroF1 = evaluation.MacroF1,
                IsBest = isBest
            };
            epochReports.Add(epochReport);
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Epoch {0}/{1}: token accuracy={2:F4}, micro F1={3:F4}, macro F1={4:F4}, best={5}",
                    epochReport.Epoch,
                    epochReport.EpochsRequested,
                    epochReport.TokenAccuracy,
                    epochReport.MicroF1,
                    epochReport.MacroF1,
                    epochReport.IsBest.ToString().ToLowerInvariant()));

            if (_options.EarlyStoppingPatience > 0 && epochsWithoutImprovement >= _options.EarlyStoppingPatience)
            {
                earlyStoppingTriggered = true;
                break;
            }
        }

        if (validationDisabled)
            Console.WriteLine("Best model by Macro F1 is unavailable because validation split is disabled.");

        var modelToSave = validationDisabled ? model : bestModel ?? model;
        modelToSave.Save(modelPath);

        return new TrainingReport
        {
            TrainingRecords = split.Train.Count,
            ValidationRecords = split.Validation.Count,
            LabelCount = labels.Count,
            TokenCount = split.Train.Sum(s => s.Tokens.Count),
            EpochsRequested = _options.Epochs,
            EpochsCompleted = epochsCompleted,
            LearningRate = _options.LearningRate,
            L2 = _options.L2,
            Seed = _options.Seed,
            ValidationShare = _options.ValidationShare,
            EarlyStoppingPatience = _options.EarlyStoppingPatience,
            BestEpoch = bestEpoch,
            BestMacroF1 = bestEvaluation?.MacroF1,
            BestMicroF1 = bestEvaluation?.MicroF1,
            BestTokenAccuracy = bestEvaluation?.Accuracy,
            EarlyStoppingTriggered = earlyStoppingTriggered,
            ValidationDisabled = validationDisabled,
            ModelPath = modelPath,
            Evaluation = bestEvaluation,
            Epochs = epochReports
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
        var validationCount = _options.ValidationShare == 0
            ? 0
            : Math.Max(1, (int)Math.Round(shuffled.Length * _options.ValidationShare));
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
