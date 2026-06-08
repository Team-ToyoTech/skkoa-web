@echo off
setlocal EnableExtensions

set "BASE_URL=%SKKOA_BASE_URL%"
if "%BASE_URL%"=="" set "BASE_URL=https://skkoa.toyotech.dev/compiler"

set "LOCAL_PS1=%~dp0skkoa-windows.ps1"
set "PS1_FILE=%TEMP%\skkoa-windows-installer-%RANDOM%.ps1"

if exist "%LOCAL_PS1%" (
    set "PS1_FILE=%LOCAL_PS1%"
) else (
    echo [skkoa] Downloading Windows installer...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest -UseBasicParsing '%BASE_URL%/download/skkoa-windows.ps1' -OutFile '%PS1_FILE%'"
    if errorlevel 1 (
        echo [skkoa] Failed to download installer script.
        exit /b 1
    )
)

echo [skkoa] Running installer...
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1_FILE%"
set "RESULT=%ERRORLEVEL%"
if not "%PS1_FILE%"=="%LOCAL_PS1%" del "%PS1_FILE%" >nul 2>nul

if not "%RESULT%"=="0" (
    echo [skkoa] Installation failed.
    exit /b %RESULT%
)

echo [skkoa] Done. Open a new terminal and run: skkoa hello.koa
exit /b 0
