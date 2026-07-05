param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$InstallerRoot = Join-Path $Root "installer"
$EditorArtifact = Join-Path $Root "editor\artifacts\SKKOA-Studio-win-x64"
$OutputDir = Join-Path $InstallerRoot "output"
$IssPath = Join-Path $InstallerRoot "src\SkkoaStudio.Installer\SkkoaStudioInstaller.iss"
$ToolsDir = Join-Path $InstallerRoot ".tools"
$LocalInnoDir = Join-Path $ToolsDir "inno"

function Write-Step($Message) {
    Write-Host "[skkoa-installer] $Message"
}

function Invoke-Native($FilePath, [string[]]$ArgumentList, $FailureMessage) {
    if (!(Test-Path $FilePath)) {
        throw "$FailureMessage Missing executable: $FilePath"
    }
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE"
    }
}

function Find-Python {
    $Command = Get-Command python -ErrorAction SilentlyContinue
    if ($Command) {
        return @($Command.Source)
    }
    $Launcher = Get-Command py -ErrorAction SilentlyContinue
    if ($Launcher) {
        return @($Launcher.Source, "-3")
    }
    return $null
}

function Assert-RequiredFile($Path, $Message) {
    if (!(Test-Path $Path)) {
        throw "$Message Missing file: $Path"
    }
}

function Find-Iscc {
    $Command = Get-Command iscc -ErrorAction SilentlyContinue
    if ($Command) {
        return $Command.Source
    }
    foreach ($Path in @(
        (Join-Path $LocalInnoDir "ISCC.exe"),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )) {
        if ($Path -and (Test-Path $Path)) {
            return $Path
        }
    }
    return $null
}

function Install-LocalInnoSetup {
    New-Item -ItemType Directory -Force -Path $ToolsDir | Out-Null
    $InstallerPath = Join-Path $ToolsDir "innosetup-6.7.3.exe"
    $PartialPath = "$InstallerPath.partial"
    $Url = "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe"
    if (!(Test-Path $InstallerPath)) {
        Write-Step "Downloading Inno Setup 6.7.3"
        try {
            Remove-Item -LiteralPath $PartialPath -Force -ErrorAction SilentlyContinue
            Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $PartialPath
            Move-Item -LiteralPath $PartialPath -Destination $InstallerPath -Force
        }
        catch {
            Remove-Item -LiteralPath $PartialPath -Force -ErrorAction SilentlyContinue
            throw "Failed to download Inno Setup from $Url. $($_.Exception.Message)"
        }
    }
    Write-Step "Installing Inno Setup locally: $LocalInnoDir"
    Invoke-Native $InstallerPath @("/SP-", "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CURRENTUSER", "/DIR=$LocalInnoDir") "Local Inno Setup install failed."
}

Assert-RequiredFile (Join-Path $EditorArtifact "SkkoaStudio.exe") "Editor publish output was not found. Run skkoa-studio\build-editor.ps1 first."
Assert-RequiredFile (Join-Path $EditorArtifact "tools\skkoa\skkoa.exe") "Bundled SKKOA compiler was not found in the editor artifact."
Assert-RequiredFile (Join-Path $EditorArtifact "assets\icons\skkoa.ico") "Editor icon was not found in the editor artifact."

Write-Step "Generating icons"
$Python = @(Find-Python)
if (!$Python) {
    throw "Python was not found. Install Python 3 with Pillow or make python.exe available on PATH."
}
$IconScript = Join-Path $InstallerRoot "scripts\generate-icons.py"
Assert-RequiredFile $IconScript "Installer icon generation script was not found."
$PythonArgs = @()
if ($Python.Length -gt 1) {
    $PythonArgs += $Python[1..($Python.Length - 1)]
}
$PythonArgs += $IconScript
Invoke-Native $Python[0] $PythonArgs "Installer icon generation failed."
Assert-RequiredFile (Join-Path $InstallerRoot "assets\icons\skkoa-installer.ico") "Installer icon generation did not produce the setup icon."
Assert-RequiredFile (Join-Path $InstallerRoot "assets\wizard\skkoa-wizard.bmp") "Installer wizard image generation failed."
Assert-RequiredFile (Join-Path $InstallerRoot "assets\wizard\skkoa-wizard-small.bmp") "Installer wizard small image generation failed."

$Iscc = Find-Iscc
if (!$Iscc) {
    try {
        Install-LocalInnoSetup
        $Iscc = Find-Iscc
    }
    catch {
        Write-Step $_.Exception.Message
        $Choco = Get-Command choco -ErrorAction SilentlyContinue
        if ($Choco) {
            Write-Step "Trying Chocolatey fallback for Inno Setup"
            & $Choco.Source install innosetup -y --no-progress --no-color
            $Iscc = Find-Iscc
        }
    }
}
if (!$Iscc) {
    throw "Inno Setup compiler ISCC.exe was not found. Install Inno Setup 6 or run with Chocolatey available."
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Write-Step "Building installer with $Iscc"
Assert-RequiredFile $IssPath "Installer script was not found."
Invoke-Native $Iscc @("/DSourceDir=$EditorArtifact", "/DOutputDir=$OutputDir", $IssPath) "Installer build failed."

$Setup = Join-Path $OutputDir "SKKOA-Studio-Setup-x64.exe"
Assert-RequiredFile $Setup "Installer output missing."
if ((Get-Item -LiteralPath $Setup).Length -le 0) {
    throw "Installer output was created but is empty: $Setup"
}

Write-Step "Installer output: $Setup"
