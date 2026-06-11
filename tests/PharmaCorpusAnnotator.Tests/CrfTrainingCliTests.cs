using CrfDemo.Corpus;
using CrfDemo.ConsoleUi;
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
    public void Evaluation_SelectionMacroF1_ExcludesO()
    {
        var workflow = new CrfTrainingWorkflow(new TrainingOptions { ValidationShare = 0 });
        var model = Model("O", "B-STRENGTH");
        var validation = new[]
        {
            Sequence("good-1", "O"),
            Sequence("good-2", "O"),
            Sequence("missed", "B-STRENGTH")
        };

        var report = workflow.Evaluate(model, validation, validation.Length);

        report.MacroF1.Should().BeApproximately(0.4, 1e-9);
        report.SelectionMacroF1.Should().Be(0);
        report.SelectionLabels.Should().Equal("B-STRENGTH");
    }

    [Fact]
    public void Evaluation_SelectionMacroF1_ExcludesLabelsWithoutSupport()
    {
        var workflow = new CrfTrainingWorkflow(new TrainingOptions { ValidationShare = 0 });
        var model = Model("O", "B-STRENGTH", "B-ROUTE");
        var validation = new[] { Sequence("missed", "B-STRENGTH") };

        var report = workflow.Evaluate(model, validation, validation.Length);

        report.SelectionLabels.Should().Equal("B-STRENGTH");
        report.SelectionLabels.Should().NotContain("B-ROUTE");
    }

    [Fact]
    public void Evaluation_SelectionMacroF1_IncludesLabelsWithSupport()
    {
        var workflow = new CrfTrainingWorkflow(new TrainingOptions { ValidationShare = 0 });
        var model = Model("B-STRENGTH", "O");
        var validation = new[] { Sequence("matched", "B-STRENGTH") };

        var report = workflow.Evaluate(model, validation, validation.Length);

        report.SelectionLabels.Should().Equal("B-STRENGTH");
        report.SelectionMacroF1.Should().Be(1);
    }

    [Fact]
    public void Evaluation_SelectionMacroF1_IsZeroWhenValidationHasOnlyO()
    {
        var workflow = new CrfTrainingWorkflow(new TrainingOptions { ValidationShare = 0 });
        var model = Model("O", "B-STRENGTH");
        var validation = new[] { Sequence("only-o", "O") };

        var report = workflow.Evaluate(model, validation, validation.Length);

        report.SelectionMacroF1.Should().Be(0);
        report.SelectionLabels.Should().BeEmpty();
    }

    [Fact]
    public void Evaluation_Renderer_WarnsWhenSelectionMacroF1IsUnavailable()
    {
        var report = new EvaluationReport
        {
            TotalRows = 1,
            ValidationRows = 1,
            TokenCount = 1,
            Accuracy = 1,
            MicroF1 = 1,
            MacroF1 = 1,
            SelectionMacroF1 = 0,
            SelectionLabels = Array.Empty<string>(),
            Labels = new Dictionary<string, LabelMetrics>(StringComparer.Ordinal)
            {
                ["O"] = new(1, 0, 0)
            }
        };
        using var writer = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(writer);
            new ConsoleRenderer().RenderEvaluation(report, includeErrors: false);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        writer.ToString().Should().Contain("Selection Macro F1 is unavailable because validation set has no non-O labels with support.");
    }

    [Fact]
    public void Workflow_WithValidation_SavesBestSelectionMacroF1Model()
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
            savedEvaluation.SelectionMacroF1.Should().BeApproximately(report.BestSelectionMacroF1!.Value, 1e-9);
            report.BestSelectionMacroF1.Should().Be(report.Epochs.Max(x => x.SelectionMacroF1));
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
            report.BestSelectionMacroF1.Should().BeNull();
            report.Evaluation.Should().BeNull();
        }
        finally
        {
            if (File.Exists(modelPath))
                File.Delete(modelPath);
        }
    }

    [Fact]
    public void Workflow_EarlyStopping_UsesSelectionMacroF1()
    {
        var corpus = Corpus(
            Record(1, "alpha", "O"),
            Record(2, "beta", "O"),
            Record(3, "gamma", "O"));
        var modelPath = Path.Combine(Path.GetTempPath(), $"crf-early-stop-{Guid.NewGuid()}.model");

        try
        {
            var report = new CrfTrainingWorkflow(new TrainingOptions
            {
                Epochs = 5,
                Seed = 1,
                ValidationShare = 0.5,
                EarlyStoppingPatience = 1
            }).Train(corpus, modelPath);

            report.EarlyStoppingTriggered.Should().BeTrue();
            report.EpochsCompleted.Should().Be(2);
            report.BestSelectionMacroF1.Should().Be(0);
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

    private static TrainedSequenceLabeler Model(params string[] labels)
    {
        return new TrainedSequenceLabeler(
            labels,
            new Dictionary<string, double>(StringComparer.Ordinal),
            new Dictionary<string, double>(StringComparer.Ordinal));
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
