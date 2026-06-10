namespace PharmaCorpusAnnotator.Core.Models;

public sealed record AnnotationRunnerOptions(
    CsvSourceOptions CsvOptions,
    string OutputPath,
    string FailedOutputPath,
    string Schema,
    bool Resume,
    bool DryRun,
    bool Verbose);
