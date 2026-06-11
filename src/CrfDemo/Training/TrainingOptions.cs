namespace CrfDemo.Training;

public sealed class TrainingOptions
{
    public int Epochs { get; init; } = 6;
    public double LearningRate { get; init; } = 0.08;
    public double L2 { get; init; } = 0.0001;
    public int Seed { get; init; } = 42;
    public double ValidationShare { get; init; } = 0.2;
    public int EarlyStoppingPatience { get; init; } = 5;
}
