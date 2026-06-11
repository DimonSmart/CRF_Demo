using CrfDemo.ConsoleUi;
using CrfDemo.Corpus;
using CrfDemo.Demo;
using CrfDemo.Inference;
using CrfDemo.Parsing;
using CrfDemo.Tokenization;
using CrfDemo.Training;
using System.Globalization;

var exitCode = Run(args);
return exitCode;

static int Run(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help")
    {
        Help();
        return 0;
    }

    try
    {
        return args[0] switch
        {
            "inspect-corpus" => InspectCorpus(args[1..]),
            "train" => Train(args[1..]),
            "tokenize" => Tokenize(args[1..]),
            "parse" => Parse(args[1..]),
            "demo" => Demo(args[1..]),
            "evaluate" => Evaluate(args[1..]),
            _ => Unknown(args[0])
        };
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int InspectCorpus(string[] args)
{
    var options = Args.Parse(args);
    var corpusPath = options.Required("--corpus");
    var corpus = CorpusLoader.Load(corpusPath);
    var validation = CorpusValidator.Validate(corpus);
    var stats = CorpusStatisticsBuilder.Build(corpus);
    new ConsoleRenderer().RenderCorpusReport(corpusPath, corpus, stats, validation);
    return validation.IsValidForTraining ? 0 : 2;
}

static int Train(string[] args)
{
    var options = Args.Parse(args);
    var corpusPath = options.Required("--corpus");
    var modelPath = options.Required("--model");
    var corpus = CorpusLoader.Load(corpusPath);
    var validation = CorpusValidator.Validate(corpus);
    if (!validation.IsValidForTraining)
    {
        new ConsoleRenderer().WriteValidation(validation);
        return 2;
    }

    var trainingOptions = TrainingOptionsParser.Parse(options);
    var report = new CrfTrainingWorkflow(trainingOptions).Train(corpus, modelPath);
    new ConsoleRenderer().RenderTrainingReport(report);
    return 0;
}

static int Tokenize(string[] args)
{
    var text = Args.Parse(args).Required("--text");
    var tokens = new PharmaTokenizer().Tokenize(text).Tokens.Select(t => (t.Index, t.Text)).ToArray();
    new ConsoleRenderer().RenderTokenized(tokens);
    return 0;
}

static int Parse(string[] args)
{
    var options = Args.Parse(args);
    var modelPath = options.Required("--model");
    var text = options.Required("--text");
    var verbose = options.Contains("--verbose");
    var model = TrainedSequenceLabeler.Load(modelPath);
    var tagged = new CrfPredictor(model).Predict(text);
    var parsed = new PharmaLineAssembler().Assemble(text, tagged);
    new ConsoleRenderer().RenderParseResult(parsed, verbose);
    return 0;
}

static int Demo(string[] args)
{
    var options = Args.Parse(args);
    var modelPath = options.Required("--model");
    _ = options.Get("--corpus");
    new DemoRunner().Run(TrainedSequenceLabeler.Load(modelPath));
    return 0;
}

static int Evaluate(string[] args)
{
    var options = Args.Parse(args);
    var corpusPath = options.Required("--corpus");
    var modelPath = options.Required("--model");
    var trainingOptions = TrainingOptionsParser.Parse(options);
    var corpus = CorpusLoader.Load(corpusPath);
    var sequences = TrainingDataBuilder.Build(corpus);
    var workflow = new CrfTrainingWorkflow(trainingOptions);
    var split = workflow.Split(sequences);
    var report = workflow.Evaluate(TrainedSequenceLabeler.Load(modelPath), split.Validation, sequences.Count);
    new ConsoleRenderer().RenderEvaluation(report, includeErrors: true);
    return 0;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Help();
    return 1;
}

static void Help()
{
    Console.WriteLine("crf-demo inspect-corpus --corpus corpus/pharma-corpus.json");
    Console.WriteLine("crf-demo train --corpus corpus/pharma-corpus.json --model models/pharma-crf.model [--epochs 40 --learning-rate 0.03 --l2 0.001 --seed 42 --validation-share 0.2 --early-stopping-patience 5]");
    Console.WriteLine("crf-demo tokenize --text \"CITALOPRAM NORMON 20MG 28 COMPRIMIDOS EFG\"");
    Console.WriteLine("crf-demo parse --model models/pharma-crf.model --text \"CITALOPRAM NORMON 20MG 28 COMPRIMIDOS EFG\"");
    Console.WriteLine("crf-demo demo --corpus corpus/pharma-corpus.json --model models/pharma-crf.model");
    Console.WriteLine("crf-demo evaluate --corpus corpus/pharma-corpus.json --model models/pharma-crf.model");
}

public static class TrainingOptionsParser
{
    public static TrainingOptions Parse(Args args)
    {
        var defaults = new TrainingOptions();
        var options = new TrainingOptions
        {
            Epochs = args.GetInt("--epochs", defaults.Epochs),
            LearningRate = args.GetDouble("--learning-rate", defaults.LearningRate),
            L2 = args.GetDouble("--l2", defaults.L2),
            Seed = args.GetInt("--seed", defaults.Seed),
            ValidationShare = args.GetDouble("--validation-share", defaults.ValidationShare),
            EarlyStoppingPatience = args.GetInt("--early-stopping-patience", defaults.EarlyStoppingPatience)
        };

        Validate(options);
        return options;
    }

    public static void Validate(TrainingOptions options)
    {
        if (options.Epochs <= 0)
            throw new ArgumentException("--epochs must be greater than 0.");
        if (options.LearningRate <= 0)
            throw new ArgumentException("--learning-rate must be greater than 0.");
        if (options.L2 < 0)
            throw new ArgumentException("--l2 must be greater than or equal to 0.");
        if (options.ValidationShare < 0)
            throw new ArgumentException("--validation-share must be greater than or equal to 0.");
        if (options.ValidationShare >= 1)
            throw new ArgumentException("--validation-share must be less than 1.");
        if (options.EarlyStoppingPatience < 0)
            throw new ArgumentException("--early-stopping-patience must be greater than or equal to 0.");
    }
}

public sealed class Args
{
    private readonly Dictionary<string, string?> _values;

    private Args(Dictionary<string, string?> values)
    {
        _values = values;
    }

    public static Args Parse(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--", StringComparison.Ordinal))
                continue;

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                values[key] = args[++i];
            else
                values[key] = null;
        }

        return new Args(values);
    }

    public string Required(string key)
    {
        if (!_values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Required option is missing: {key}");

        return value;
    }

    public string? Get(string key) => _values.GetValueOrDefault(key);
    public int GetInt(string key, int defaultValue)
    {
        var value = Get(key);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new ArgumentException($"{key} must be an integer.");
    }

    public double GetDouble(string key, double defaultValue)
    {
        var value = Get(key);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new ArgumentException($"{key} must be a number with '.' as decimal separator.");
    }

    public bool Contains(string key) => _values.ContainsKey(key);
}
