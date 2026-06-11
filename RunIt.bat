@echo off
setlocal

cd /d C:\Private\CRF_Demo

set LLM_MODEL=

dotnet run --project C:\Private\CRF_Demo\src\PharmaCorpusAnnotator.Cli -- annotate ^
  --input C:\Private\CRF_Demo\SourceData\20260610_Nomenclator_de_Facturacion.csv ^
  --text-column "Nombre del producto farmacéutico" ^
  --require-non-empty-column "Principio activo o asociación de principios activos" ^
  --source-key nomenclator-facturacion-20260610 ^
  --max-rows 100 ^
  --output C:\Private\CRF_Demo\corpus\pharma-corpus.json ^
  --failed-output C:\Private\CRF_Demo\corpus\pharma-corpus.failed.jsonl ^
  --llm-profile ollama ^
  --verbose

endlocal
