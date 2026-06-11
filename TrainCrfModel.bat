@echo off
setlocal

set "ROOT=%~dp0"
set "CORPUS=%ROOT%corpus\pharma-corpus.json"
set "MODEL_DIR=%ROOT%models"
set "MODEL=%MODEL_DIR%\pharma-crf.model"
set "EPOCHS=40"
set "LEARNING_RATE=0.03"
set "L2=0.001"
set "SEED=42"
set "VALIDATION_SHARE=0.2"
set "EARLY_STOPPING_PATIENCE=5"

cd /d "%ROOT%"

if not exist "%MODEL_DIR%" mkdir "%MODEL_DIR%"

dotnet run --project "%ROOT%src\CrfDemo" -- train ^
  --corpus "%CORPUS%" ^
  --model "%MODEL%" ^
  --epochs "%EPOCHS%" ^
  --learning-rate "%LEARNING_RATE%" ^
  --l2 "%L2%" ^
  --seed "%SEED%" ^
  --validation-share "%VALIDATION_SHARE%" ^
  --early-stopping-patience "%EARLY_STOPPING_PATIENCE%" ^
  %*

exit /b %errorlevel%
