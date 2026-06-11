@echo off
setlocal

set "ROOT=%~dp0"
set "CORPUS=%ROOT%corpus\pharma-corpus.json"
set "MODEL_DIR=%ROOT%models"
set "MODEL=%MODEL_DIR%\pharma-crf.model"

cd /d "%ROOT%"

if not exist "%MODEL_DIR%" mkdir "%MODEL_DIR%"

dotnet run --project "%ROOT%src\CrfDemo" -- train ^
  --corpus "%CORPUS%" ^
  --model "%MODEL%" ^
  %*

exit /b %errorlevel%
