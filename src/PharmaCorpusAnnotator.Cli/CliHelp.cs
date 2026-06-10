namespace PharmaCorpusAnnotator.Cli;

public static class CliHelp
{
    public static void Print()
    {
        Console.WriteLine("""
            pharma-annotate - Pharmaceutical corpus annotator

            Commands:
              annotate    Annotate a pharmaceutical CSV file

            Options for 'annotate':
              --input <path>            Path to input CSV file (required)
              --output <path>           Path to output corpus JSON (required)
              --text-column <name>      Column with text to annotate (default: "Nombre del producto farmacéutico")
              --source-key <key>        Stable source key (default: slug from file name)
              --delimiter <char>        CSV delimiter (default: ";")
              --encoding <enc>          Input encoding (default: "utf-8-sig")
              --context-columns <list>  Comma-separated context columns (default: spec defaults)
              --max-rows <n>            Maximum rows to process
              --skip <n>                Rows to skip (default: 0)
              --resume                  Skip already-present rows in output (default: true)
              --no-resume               Disable resume
              --failed-output <path>    Path to failed records JSONL
              --attempts-output <path>  Path to LLM attempt diagnostics JSONL
              --verbose                 Verbose diagnostics
              --dry-run                 Tokenize without calling LLM

            Environment variables:
              LLM_MODEL          (default: qwen3:14b)
              LLM_BASE_URL       (default: http://localhost:11434)
              LLM_API_KEY        (default: ollama)
              LLM_RETRY_COUNT    (default: 5)
              LLM_TIMEOUT_MINUTES (default: 30)
              LLM_IGNORE_SSL_ERRORS
              LLM_USERNAME
              LLM_PASSWORD
            """);
    }
}
