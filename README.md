# PharmaCorpusAnnotator

Creates a token-level annotated corpus from Spanish pharmaceutical CSV files.

It uses an OpenAI-compatible LLM endpoint (for example Ollama) through Microsoft Agentic Framework typed responses. The LLM receives pre-tokenized input and returns a BIO label array; the app validates the labels and builds the final `PharmaAnnotationResponse` used by the corpus pipeline.

The output is a source-blocked JSON corpus:
- source metadata is written once per source block
- every record contains `rowNumber`, source `text`, `context`, and `annotation`
- `Código Nacional` is preserved as context, not as a technical identifier

## Purpose

Prepares a test corpus for CRF / sequence labeling on pharmaceutical product name strings. Does **not** train a CRF model yet. The JSON corpus can later be converted to CoNLL/BIO format for use with sequence labeling frameworks.

## Input CSV expectations

- Encoding: UTF-8 with BOM (`utf-8-sig`)
- Delimiter: `;`
- Header on row 1, data from row 2
- Default text column: `Nombre del producto farmacéutico`

## Output corpus format

```json
{
  "schemaVersion": "1.0",
  "createdAt": "2026-06-10T12:00:00Z",
  "annotationSchema": { "language": "es", "labels": ["O", "B-AI", "I-AI", ...] },
  "sources": [
    {
      "source": {
        "sourceKey": "nomenclator-facturacion-20260610",
        "fileName": "20260610_Nomenclator_de_Facturacion.csv",
        "format": "csv",
        "encoding": "utf-8-sig",
        "delimiter": ";",
        "textColumn": "Nombre del producto farmacéutico",
        "contextColumns": ["Código Nacional", ...]
      },
      "records": [
        {
          "rowNumber": 2,
          "text": "captopril 4 mg/ml suspension oral 100 ml 1 frasco",
          "context": { "Código Nacional": "140002" },
          "annotation": { "tokens": [...], "normalized": {...}, "quality": {...} }
        }
      ]
    }
  ]
}
```

## Prerequisites

### Ollama

1. Install [Ollama](https://ollama.com)
2. Pull a model:
   ```
   ollama pull qwen3:14b
   ```
3. Start the server (runs on port 11434 by default)

## Quick start

### PowerShell

```powershell
$env:LLM_BASE_URL = "http://localhost:11434"
$env:LLM_MODEL    = "qwen3:14b"
$env:LLM_API_KEY  = "ollama"

dotnet run --project src/PharmaCorpusAnnotator.Cli -- annotate `
  --input SourceData/20260610_Nomenclator_de_Facturacion.csv `
  --text-column "Nombre del producto farmacéutico" `
  --source-key nomenclator-facturacion-20260610 `
  --max-rows 50 `
  --output corpus/pharma-corpus.json
```

### Windows CMD

```bat
set LLM_BASE_URL=http://localhost:11434
set LLM_MODEL=qwen3:14b
set LLM_API_KEY=ollama

dotnet run --project src/PharmaCorpusAnnotator.Cli -- annotate ^
  --input SourceData/20260610_Nomenclator_de_Facturacion.csv ^
  --text-column "Nombre del producto farmacéutico" ^
  --source-key nomenclator-facturacion-20260610 ^
  --max-rows 50 ^
  --output corpus/pharma-corpus.json
```

### Dry run (no LLM)

```powershell
dotnet run --project src/PharmaCorpusAnnotator.Cli -- annotate `
  --input SourceData/20260610_Nomenclator_de_Facturacion.csv `
  --output corpus/pharma-corpus.json `
  --max-rows 10 `
  --dry-run
```

## CLI options

| Option | Default | Description |
|--------|---------|-------------|
| `--input` | required | Path to input CSV |
| `--output` | required | Path to output corpus JSON |
| `--text-column` | `Nombre del producto farmacéutico` | Column with text to annotate |
| `--source-key` | slug from filename | Stable key for the source block |
| `--delimiter` | `;` | CSV field delimiter |
| `--encoding` | `utf-8-sig` | Input encoding |
| `--context-columns` | spec defaults | Comma-separated context columns |
| `--max-rows` | all | Maximum rows to process |
| `--skip` | `0` | Rows to skip before processing |
| `--resume` / `--no-resume` | resume on | Skip rows already in output |
| `--failed-output` | `*.failed.jsonl` | Path for failed records |
| `--attempts-output` | — | Path for LLM attempt diagnostics JSONL |
| `--verbose` | off | Verbose diagnostics |
| `--dry-run` | off | Tokenize without LLM calls |

## Environment variables

| Variable | Default |
|----------|---------|
| `LLM_MODEL` | `qwen3:14b` |
| `LLM_BASE_URL` | `http://localhost:11434` |
| `LLM_API_KEY` | `ollama` |
| `LLM_RETRY_COUNT` | `5` |
| `LLM_TIMEOUT_MINUTES` | `30` |
| `LLM_IGNORE_SSL_ERRORS` | `false` |
| `LLM_USERNAME` | — |
| `LLM_PASSWORD` | — |

## Building and testing

```bash
dotnet build
dotnet test
```

### Run explicit LLM integration test

```powershell
$env:LLM_BASE_URL = "http://localhost:11434"
$env:LLM_MODEL = "qwen3:14b"
$env:LLM_API_KEY = "ollama"

dotnet test --filter "FullyQualifiedName~LlmIntegrationTests" -- xUnit.Explicit=on
```

## Future export

The JSON corpus is designed to be easily converted to CoNLL/BIO format:
```
captopril  B-AI
4          B-ST
mg/ml      I-ST
suspension B-DF
...
```

A CoNLL exporter is planned but not yet implemented (non-goal for v1).
