using CrfDemo.Inference;

namespace CrfDemo.Training;

public interface ISequenceLabelerTrainer
{
    TrainedSequenceLabeler Train(IReadOnlyList<TrainingSequence> sequences, IReadOnlyList<string> labels);
}
