$ErrorActionPreference = "Stop"
$Script = Join-Path $PSScriptRoot "generate-icons.py"

$Python = Get-Command python -ErrorAction SilentlyContinue
if (-not $Python) {
    $PyLauncher = Get-Command py -ErrorAction SilentlyContinue
    if (-not $PyLauncher) {
        throw "Python was not found. Install Python 3 with Pillow or make python.exe available on PATH."
    }
    & $PyLauncher.Source -3 $Script
    if ($LASTEXITCODE -ne 0) {
        throw "Installer icon generation failed with exit code $LASTEXITCODE."
    }
    exit 0
}

& $Python.Source $Script
if ($LASTEXITCODE -ne 0) {
    throw "Installer icon generation failed with exit code $LASTEXITCODE."
}
