@echo off
setlocal

set "ROOT=%~dp0"
set "MODEL=%ROOT%models\pharma-crf.model"
set "DEFAULT_TEXT=CITALOPRAM NORMON 20MG 28 COMPRIMIDOS EFG"

cd /d "%ROOT%"

if "%~1"=="" (
  set "TEXT=%DEFAULT_TEXT%"
) else (
  set "TEXT=%*"
)

dotnet run --project "%ROOT%src\CrfDemo" -- parse ^
  --model "%MODEL%" ^
  --text "%TEXT%"

exit /b %errorlevel%
