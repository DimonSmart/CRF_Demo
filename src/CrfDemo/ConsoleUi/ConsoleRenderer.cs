using CrfDemo.Corpus;
using CrfDemo.Inference;
using CrfDemo.Parsing;
using CrfDemo.Training;
using System.Globalization;

namespace CrfDemo.ConsoleUi;

public sealed class ConsoleRenderer
{
    public void RenderCorpusReport(string path, CorpusDocument corpus, CorpusStatistics stats, CorpusValidationReport validation)
    {
        Console.WriteLine($"Corpus file: {path}");
        Console.WriteLine($"Schema version: {corpus.SchemaVersion}");
        Console.WriteLine($"Created at: {corpus.CreatedAt}");
        Console.WriteLine($"Sources: {stats.SourceCount}");
        Console.WriteLine($"Records: {stats.RecordCount}");
        Console.WriteLine($"Tokens: {stats.TokenCount}");
        Console.WriteLine($"Labels: {stats.LabelCount}");
        Console.WriteLine($"Token length: min {stats.MinTokens}, avg {stats.AverageTokens:F1}, max {stats.MaxTokens}");
        Console.WriteLine($"Records with warnings: {stats.RecordsWithWarnings}");
        Console.WriteLine($"BIO errors: {validation.BioErrors}");
        Console.WriteLine($"Unknown labels: {validation.UnknownLabelErrors}");
        Console.WriteLine();
        Console.WriteLine("Label distribution:");
        foreach (var item in stats.LabelDistribution.OrderByDescending(x => x.Value))
            Console.WriteLine($"  {item.Key,-28} {item.Value}");

        WriteValidation(validation);
    }

    public void RenderTrainingReport(TrainingReport report)
    {
        Console.WriteLine($"Training records: {report.TrainingRecords}");
        Console.WriteLine($"Validation records: {report.ValidationRecords}");
        Console.WriteLine($"Epochs requested: {report.EpochsRequested}");
        Console.WriteLine($"Epochs completed: {report.EpochsCompleted}");
        Console.WriteLine($"Learning rate: {FormatDouble(report.LearningRate)}");
        Console.WriteLine($"L2: {FormatDouble(report.L2)}");
        Console.WriteLine($"Seed: {report.Seed}");
        Console.WriteLine($"Validation share: {FormatDouble(report.ValidationShare)}");
        Console.WriteLine($"Early stopping patience: {report.EarlyStoppingPatience}");
        Console.WriteLine($"Best epoch: {report.BestEpoch?.ToString() ?? "n/a"}");
        Console.WriteLine($"Best Macro F1: {FormatMetric(report.BestMacroF1)}");
        Console.WriteLine($"Best Micro F1: {FormatMetric(report.BestMicroF1)}");
        Console.WriteLine($"Best Token Accuracy: {FormatMetric(report.BestTokenAccuracy)}");
        Console.WriteLine($"Labels: {report.LabelCount}");
        Console.WriteLine($"Tokens: {report.TokenCount}");
        Console.WriteLine($"Model path: {report.ModelPath}");
        Console.WriteLine("Status: saved");
        if (report.ValidationDisabled)
            Console.WriteLine("Validation is disabled. The last epoch model was saved.");
        else if (report.EarlyStoppingTriggered)
            Console.WriteLine($"Early stopping: stopped after {report.EarlyStoppingPatience} epochs without Macro F1 improvement.");
        else
            Console.WriteLine("Early stopping: not triggered.");

        if (report.Evaluation is not null)
            RenderEvaluation(report.Evaluation, includeErrors: false);
    }

    public void RenderEvaluation(EvaluationReport report, bool includeErrors)
    {
        Console.WriteLine();
        Console.WriteLine($"Total rows: {report.TotalRows}");
        Console.WriteLine($"Validation rows: {report.ValidationRows}");
        Console.WriteLine($"Tokens: {report.TokenCount}");
        Console.WriteLine($"Token accuracy: {FormatPercent(report.Accuracy)}");
        Console.WriteLine($"Micro F1: {FormatPercent(report.MicroF1)}");
        Console.WriteLine($"Macro F1: {FormatPercent(report.MacroF1)}");
        Console.WriteLine();
        Console.WriteLine($"{"Label",-28} {"Precision",10} {"Recall",10} {"F1",10}");
        foreach (var item in report.Labels)
            Console.WriteLine($"{item.Key,-28} {FormatPercent(item.Value.Precision),10} {FormatPercent(item.Value.Recall),10} {FormatPercent(item.Value.F1),10}");

        if (includeErrors)
            RenderErrors(report.Errors);
    }

    public void RenderParseResult(ParsedPharmaLine parsed, bool verbose)
    {
        Console.WriteLine("Input:");
        Console.WriteLine(parsed.OriginalText);
        Console.WriteLine();
        Console.WriteLine("Colored line:");
        foreach (var token in parsed.Tokens)
        {
            Console.ForegroundColor = LabelColorMap.ColorForEntity(token.Entity);
            Console.Write($"[{token.Token}] ");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine();
        RenderTokenTable(parsed.Tokens);
        Console.WriteLine();
        RenderCard(parsed, verbose);
    }

    public void RenderTokenized(IReadOnlyList<(int Index, string Text)> tokens)
    {
        foreach (var token in tokens)
            Console.WriteLine($"{token.Index,-4}{token.Text}");
    }

    public void WriteValidation(CorpusValidationReport validation)
    {
        if (validation.CriticalErrors.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Critical errors:");
            Console.ResetColor();
            foreach (var error in validation.CriticalErrors.Take(20))
                Console.WriteLine($"  {error}");
        }

        if (validation.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Warnings:");
            Console.ResetColor();
            foreach (var warning in validation.Warnings.Take(20))
                Console.WriteLine($"  {warning}");
        }
    }

    private static void RenderTokenTable(IReadOnlyList<TaggedToken> tokens)
    {
        Console.WriteLine($"{"Index",-7} {"Token",-22} {"Label",-28} {"Entity",-22}");
        Console.WriteLine(new string('-', 82));
        foreach (var token in tokens)
            Console.WriteLine($"{token.Index,-7} {token.Token,-22} {token.Label,-28} {token.Entity,-22}");
    }

    private static void RenderCard(ParsedPharmaLine parsed, bool verbose)
    {
        Console.WriteLine("Structured result:");
        WriteField("Active ingredients", string.Join(", ", parsed.ActiveIngredients), verbose);
        WriteField("Strength", string.Join(", ", parsed.Strengths), verbose);
        WriteField("Dose form", parsed.DoseForm, verbose);
        WriteField("Route", parsed.Route, verbose);
        WriteField("Package volume", parsed.PackageVolume, verbose);
        WriteField("Package quantity", parsed.PackageQuantity?.ToString() ?? parsed.PackageQuantityText, verbose);
        WriteField("Package unit", parsed.PackageUnit, verbose);
        WriteField("Regulatory markers", string.Join(", ", parsed.RegulatoryMarkers), verbose);
        WriteField("Other tokens", string.Join(" ", parsed.OtherTokens), verbose);

        foreach (var warning in parsed.Warnings)
            Console.WriteLine($"Warning: {warning}");
    }

    private static void WriteField(string name, string? value, bool verbose)
    {
        if (verbose || !string.IsNullOrWhiteSpace(value))
            Console.WriteLine($"{name}: {value}");
    }

    private static void RenderErrors(IReadOnlyList<PredictionError> errors)
    {
        foreach (var error in errors)
        {
            Console.WriteLine();
            Console.WriteLine("Text:");
            Console.WriteLine(error.Text);
            Console.WriteLine($"{"Token",-22} {"Expected",-28} {"Predicted",-28}");
            foreach (var token in error.Tokens)
                Console.WriteLine($"{token.Token,-22} {token.Expected,-28} {token.Predicted,-28}");
        }
    }

    private static string FormatMetric(double? value) => value.HasValue ? value.Value.ToString("F4", CultureInfo.InvariantCulture) : "n/a";
    private static string FormatDouble(double value) => value.ToString("G", CultureInfo.InvariantCulture);
    private static string FormatPercent(double value) => value.ToString("P2", CultureInfo.InvariantCulture);
}
