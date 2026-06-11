# PharmaCorpusAnnotator

Creates a token-level annotated corpus from Spanish pharmaceutical CSV files.

It uses an OpenAI-compatible LLM endpoint (for example Ollama) through Microsoft Agentic Framework typed responses. The LLM receives pre-tokenized input and returns a BIO label array; the app validates the labels and builds the final `PharmaAnnotationResponse` used by the corpus pipeline.

The output is a source-blocked JSON corpus:
- source metadata is written once per source block
- every record contains `rowNumber`, source `text`, and `annotation`

## Purpose

Prepares a test corpus for CRF / sequence labeling on pharmaceutical product name strings.

The repository also contains `CrfDemo`, a console demo that loads `pharma-corpus.json`, trains a linear-chain CRF sequence labeler, tags new pharmaceutical lines, renders colored BIO labels, and assembles a structured parsed card.

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
        "textColumn": "Nombre del producto farmacéutico"
      },
      "records": [
        {
          "rowNumber": 2,
          "text": "captopril 4 mg/ml suspension oral 100 ml 1 frasco",
          "annotation": { "tokens": [...], "normalized": {...}, "quality": {...} }
        }
      ]
    }
  ]
}
```

## Prerequisites

### LLM endpoint

The annotator uses an OpenAI-compatible `/v1/chat/completions` endpoint.

Profiles are stored in `llmsettings.json`:

```json
{
  "Llm": {
    "ActiveProfile": "ollama",
    "Profiles": [
      {
        "Name": "ollama",
        "BaseUrl": "http://localhost:11434",
        "Model": "gpt-oss:120b-cloud",
        "ApiKey": "ollama"
      },
      {
        "Name": "nvidia",
        "BaseUrl": "https://integrate.api.nvidia.com/v1",
        "Model": "openai/gpt-oss-120b",
        "ApiKey": "%NVIDIA_API_KEY%"
      }
    ]
  }
}
```

Set `Llm:ActiveProfile` in `llmsettings.json`, or override it for one run. The selector accepts either a profile name or a 1-based profile number from the `Profiles` array:

```powershell
$env:LLM_PROFILE = "nvidia"
# or:
$env:LLM_PROFILE = "2"
```

If a setting value is written as `%NAME%`, the app reads the real value from .NET user-secrets or environment variables by `NAME`.

```powershell
dotnet user-secrets set NVIDIA_API_KEY "nvapi-..." --project src/PharmaCorpusAnnotator.Cli
```

For local Ollama, set the `ollama` profile model to an installed local model, for example `qwen3:14b`:

1. Install [Ollama](https://ollama.com)
2. Pull a model:
   ```
   ollama pull qwen3:14b
   ```
3. Start the server (runs on port 11434 by default)

## Quick start

### PowerShell

```powershell
$env:LLM_PROFILE = "nvidia"

dotnet run --project src/PharmaCorpusAnnotator.Cli -- annotate `
  --input SourceData/20260610_Nomenclator_de_Facturacion.csv `
  --text-column "Nombre del producto farmacéutico" `
  --source-key nomenclator-facturacion-20260610 `
  --max-rows 50 `
  --output corpus/pharma-corpus.json
```

### Windows CMD

```bat
set LLM_PROFILE=nvidia

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
| `--max-rows` | all | Maximum rows to process |
| `--skip` | `0` | Rows to skip before processing |
| `--resume` / `--no-resume` | resume on | Skip rows already in output |
| `--failed-output` | `*.failed.jsonl` | Path for failed records |
| `--attempts-output` | — | Path for LLM attempt diagnostics JSONL |
| `--verbose` | off | Verbose diagnostics |
| `--dry-run` | off | Tokenize without LLM calls |

## LLM configuration

Configuration order:

1. `llmsettings.json`
2. .NET user-secrets
3. Environment variables

Environment variables override the selected profile:

| Variable | Default |
|----------|---------|
| `LLM_PROFILE` | `Llm:ActiveProfile` or `ollama` |
| `LLM_CONFIG_PATH` | `llmsettings.json` |
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
$env:LLM_PROFILE = "nvidia"

dotnet test --filter "FullyQualifiedName~LlmIntegrationTests" -- xUnit.Explicit=on
```

## CRF demo

The current model extracts only fields represented in the BIO schema:

- active ingredients
- strengths
- dose form
- route
- package volume
- package quantity
- package unit
- regulatory markers

It does not extract price, manufacturer, brand, or drug name as separate fields because those labels are not present in the current corpus.

### Inspect corpus

```powershell
dotnet run --project src/CrfDemo -- inspect-corpus --corpus corpus/pharma-corpus.json
```

### Train CRF model

```powershell
dotnet run --project src/CrfDemo -- train --corpus corpus/pharma-corpus.json --model models/pharma-crf.model
```

or run:

```bat
TrainCrfModel.bat
```

### Training parameters

The `train` command accepts these training parameters:

| Option | Default | Description |
|--------|---------|-------------|
| `--epochs` | `6` | Number of training epochs |
| `--learning-rate` | `0.08` | Training learning rate |
| `--l2` | `0.0001` | L2 regularization |
| `--seed` | `42` | Seed for deterministic shuffle and train/validation split |
| `--validation-share` | `0.2` | Corpus share used for validation, from `0.0` inclusive to `1.0` exclusive |
| `--early-stopping-patience` | `5` | Epochs without validation Selection Macro F1 improvement before stopping; `0` disables early stopping |

Training evaluates the model after each epoch on the validation split. The final `.model` file contains the best model by validation `Selection Macro F1`. `Selection Macro F1` is calculated only over labels that are present in the validation set, excluding `O`. `Full Macro F1` remains in the report as a diagnostic metric over the full evaluated label set.

If `Full Macro F1` is noticeably lower than `Selection Macro F1`, the BIO schema may contain rare labels or labels absent from the current validation split. Model selection uses `Selection Macro F1` because it is more stable on a small corpus. If `--validation-share 0` is used, validation is disabled, `Selection Macro F1` is unavailable, and the last epoch model is saved.

```bat
TrainCrfModel.bat
```

```bat
TrainCrfModel.bat --epochs 40 --learning-rate 0.03 --l2 0.001
```

```bat
TrainCrfModel.bat --epochs 80 --learning-rate 0.015 --early-stopping-patience 10
```

Parameters passed to `TrainCrfModel.bat` override the script defaults when the same CLI option is repeated.

### Parse a new line

```powershell
dotnet run --project src/CrfDemo -- parse --model models/pharma-crf.model --text "CITALOPRAM NORMON 20MG 28 COMPRIMIDOS EFG"
```

or run:

```bat
RunCrfClassifier.bat CITALOPRAM NORMON 20MG 28 COMPRIMIDOS EFG
```

### Demo and evaluation

```powershell
dotnet run --project src/CrfDemo -- demo --corpus corpus/pharma-corpus.json --model models/pharma-crf.model
dotnet run --project src/CrfDemo -- evaluate --corpus corpus/pharma-corpus.json --model models/pharma-crf.model
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
