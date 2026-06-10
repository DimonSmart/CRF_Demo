namespace PharmaCorpusAnnotator.Core.Models;

public sealed record AnnotationRunnerOptions(
    CsvSourceOptions CsvOptions,
    string OutputPath,
    string FailedOutputPath,
    bool Resume,
    bool DryRun,
    bool Verbose);
