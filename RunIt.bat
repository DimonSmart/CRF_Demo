@echo off
setlocal

cd /d C:\Private\CRF_Demo

set LLM_BASE_URL=http://localhost:11434
set LLM_MODEL=gpt-oss:20b-cloud
set LLM_API_KEY=ollama
set LLM_RETRY_COUNT=1

dotnet run --project C:\Private\CRF_Demo\src\PharmaCorpusAnnotator.Cli -- annotate ^
  --input C:\Private\CRF_Demo\SourceData\20260610_Nomenclator_de_Facturacion.csv ^
  --text-column "Nombre del producto farmacéutico" ^
  --source-key nomenclator-facturacion-20260610 ^
  --max-rows 3 ^
  --output C:\Private\CRF_Demo\corpus\pharma-corpus.json ^
  --failed-output C:\Private\CRF_Demo\corpus\pharma-corpus.failed.jsonl ^
  --verbose

endlocal
