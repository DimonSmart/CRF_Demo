using CrfDemo.Corpus;
using CrfDemo.Inference;
using CrfDemo.Training;
using FluentAssertions;

namespace PharmaCorpusAnnotator.Tests;

public class CrfTrainingCliTests
{
    [Fact]
    public void Args_ReadsTrainingNumbers()
    {
        var args = Args.Parse(new[]
        {
            "--epochs", "80",
            "--learning-rate", "0.015",
            "--l2", "0.0005",
            "--seed", "123",
            "--validation-share", "0.25",
            "--early-stopping-patience", "10"
        });

        var options = TrainingOptionsParser.Parse(args);

        options.Epochs.Should().Be(80);
        options.LearningRate.Should().Be(0.015);
        options.L2.Should().Be(0.0005);
        options.Seed.Should().Be(123);
        options.ValidationShare.Should().Be(0.25);
        options.EarlyStoppingPatience.Should().Be(10);
    }

    [Fact]
    public void Args_LastValueWins()
    {
        var args = Args.Parse(new[] { "--epochs", "40", "--epochs", "80" });

        args.GetInt("--epochs", 6).Should().Be(80);
    }

    [Fact]
    public void TrainingOptionsParser_RejectsInvalidValues()
    {
        var args = Args.Parse(new[] { "--validation-share", "1" });

        var act = () => TrainingOptionsParser.Parse(args);

        act.Should().Throw<ArgumentException>()
            .WithMessage("--validation-share must be less than 1.");
    }

    [Fact]
    public void Workflow_UsesCliOptionsForSplit()
    {
        var workflow = new CrfTrainingWorkflow(new TrainingOptions { Seed = 7, ValidationShare = 0 });
        var sequences = new[]
        {
            Sequence("a", "O"),
            Sequence("b", "O"),
            Sequence("c", "O")
        };

        var split = workflow.Split(sequences);

        split.Train.Should().HaveCount(3);
        split.Validation.Should().BeEmpty();
    }

    [Fact]
    public void Workflow_WithValidation_SavesBestMacroF1Model()
    {
        var corpus = Corpus(
            Record(1, "alpha", "B-AI"),
            Record(2, "beta", "O"),
            Record(3, "gamma", "O"),
            Record(4, "delta", "O"));
        var modelPath = Path.Combine(Path.GetTempPath(), $"crf-best-{Guid.NewGuid()}.model");

        try
        {
            var workflow = new CrfTrainingWorkflow(new TrainingOptions
            {
                Epochs = 2,
                LearningRate = 0.05,
                Seed = 1,
                ValidationShare = 0.5,
                EarlyStoppingPatience = 0
            });

            var report = workflow.Train(corpus, modelPath);
            var saved = TrainedSequenceLabeler.Load(modelPath);
            var split = workflow.Split(TrainingDataBuilder.Build(corpus));
            var savedEvaluation = workflow.Evaluate(saved, split.Validation, split.Train.Count + split.Validation.Count);

            report.BestEpoch.Should().NotBeNull();
            savedEvaluation.MacroF1.Should().BeApproximately(report.BestMacroF1!.Value, 1e-9);
        }
        finally
        {
            if (File.Exists(modelPath))
                File.Delete(modelPath);
        }
    }

    [Fact]
    public void Workflow_WithoutValidation_SavesLastModelAndReportsIt()
    {
        var corpus = Corpus(Record(1, "alpha", "B-AI"), Record(2, "beta", "O"));
        var modelPath = Path.Combine(Path.GetTempPath(), $"crf-last-{Guid.NewGuid()}.model");

        try
        {
            var report = new CrfTrainingWorkflow(new TrainingOptions
            {
                Epochs = 1,
                ValidationShare = 0
            }).Train(corpus, modelPath);

            File.Exists(modelPath).Should().BeTrue();
            report.ValidationDisabled.Should().BeTrue();
            report.BestEpoch.Should().BeNull();
            report.Evaluation.Should().BeNull();
        }
        finally
        {
            if (File.Exists(modelPath))
                File.Delete(modelPath);
        }
    }

    private static TrainingSequence Sequence(string token, string label)
    {
        return new TrainingSequence(token, new[] { token }, new[] { label });
    }

    private static CorpusDocument Corpus(params CorpusRecord[] records)
    {
        return new CorpusDocument
        {
            AnnotationSchema = new AnnotationSchema { Labels = new[] { "O", "B-AI" } },
            Sources = new[]
            {
                new CorpusSource { Records = records }
            }
        };
    }

    private static CorpusRecord Record(int row, string token, string label)
    {
        return new CorpusRecord
        {
            RowNumber = row,
            Text = token,
            Annotation = new CorpusAnnotation
            {
                Tokens = new[]
                {
                    new CorpusToken { Index = 0, Text = token, Label = label }
                }
            }
        };
    }
}
