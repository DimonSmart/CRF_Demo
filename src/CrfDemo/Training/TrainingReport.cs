namespace CrfDemo.Training;

public sealed class TrainingReport
{
    public int TrainingRecords { get; init; }
    public int ValidationRecords { get; init; }
    public int LabelCount { get; init; }
    public int TokenCount { get; init; }
    public int EpochsRequested { get; init; }
    public int EpochsCompleted { get; init; }
    public double LearningRate { get; init; }
    public double L2 { get; init; }
    public int Seed { get; init; }
    public double ValidationShare { get; init; }
    public int EarlyStoppingPatience { get; init; }
    public int? BestEpoch { get; init; }
    public double? BestSelectionMacroF1 { get; init; }
    public double? BestMacroF1 { get; init; }
    public double? BestMicroF1 { get; init; }
    public double? BestTokenAccuracy { get; init; }
    public bool EarlyStoppingTriggered { get; init; }
    public bool ValidationDisabled { get; init; }
    public string ModelPath { get; init; } = "";
    public EvaluationReport? Evaluation { get; init; }
    public IReadOnlyList<EpochTrainingReport> Epochs { get; init; } = Array.Empty<EpochTrainingReport>();
}

public sealed class EpochTrainingReport
{
    public int Epoch { get; init; }
    public int EpochsRequested { get; init; }
    public double TokenAccuracy { get; init; }
    public double MicroF1 { get; init; }
    public double MacroF1 { get; init; }
    public double SelectionMacroF1 { get; init; }
    public bool IsBest { get; init; }
}

public sealed class EvaluationReport
{
    public int TotalRows { get; init; }
    public int ValidationRows { get; init; }
    public int TokenCount { get; init; }
    public double Accuracy { get; init; }
    public double MicroF1 { get; init; }
    public double MacroF1 { get; init; }
    public double SelectionMacroF1 { get; init; }
    public IReadOnlyList<string> SelectionLabels { get; init; } = Array.Empty<string>();
    public Dictionary<string, LabelMetrics> Labels { get; init; } = new(StringComparer.Ordinal);
    public IReadOnlyList<PredictionError> Errors { get; init; } = Array.Empty<PredictionError>();
}

public sealed record LabelMetrics(int TruePositive, int FalsePositive, int FalseNegative)
{
    public double Precision => TruePositive + FalsePositive == 0 ? 0 : (double)TruePositive / (TruePositive + FalsePositive);
    public double Recall => TruePositive + FalseNegative == 0 ? 0 : (double)TruePositive / (TruePositive + FalseNegative);
    public double F1 => Precision + Recall == 0 ? 0 : 2 * Precision * Recall / (Precision + Recall);
}

public sealed record PredictionError(string Text, IReadOnlyList<TokenPrediction> Tokens);

public sealed record TokenPrediction(string Token, string Expected, string Predicted);
