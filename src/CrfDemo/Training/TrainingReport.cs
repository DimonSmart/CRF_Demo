namespace CrfDemo.Training;

public sealed class TrainingReport
{
    public int TrainingRecords { get; init; }
    public int ValidationRecords { get; init; }
    public int LabelCount { get; init; }
    public int TokenCount { get; init; }
    public string ModelPath { get; init; } = "";
    public EvaluationReport? Evaluation { get; init; }
}

public sealed class EvaluationReport
{
    public int TotalRows { get; init; }
    public int ValidationRows { get; init; }
    public int TokenCount { get; init; }
    public double Accuracy { get; init; }
    public double MicroF1 { get; init; }
    public double MacroF1 { get; init; }
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
