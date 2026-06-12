@echo off
setlocal

set "ROOT=%~dp0"
set "MODEL=%ROOT%models\pharma-crf.model"
set "DEFAULT_TEXT=CITALOPRAM NORMON 20MG 28 COMPRIMIDOS EFG"

cd /d "%ROOT%"

if "%~1"=="" goto use_default_text

set "TEXT="
:collect_args
if "%~1"=="" goto run_classifier
if defined TEXT (
  set "TEXT=%TEXT% %~1"
) else (
  set "TEXT=%~1"
)
shift
goto collect_args

:use_default_text
set "TEXT=%DEFAULT_TEXT%"

:run_classifier

dotnet run --project "%ROOT%src\CrfDemo" -- parse ^
  --model "%MODEL%" ^
  --text "%TEXT%"

exit /b %errorlevel%
