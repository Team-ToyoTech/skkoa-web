param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $Root
$EditorRoot = Join-Path $Root "editor"
$ArtifactsRoot = Join-Path $EditorRoot "artifacts"
$PublishDir = Join-Path $ArtifactsRoot "SKKOA-Studio-win-x64"
$ZipPath = Join-Path $ArtifactsRoot "SKKOA-Studio-win-x64.zip"
$CompilerOut = Join-Path $EditorRoot "tools\skkoa\skkoa.exe"

function Write-Step($Message) {
    Write-Host "[skkoa-studio] $Message"
}

function Find-RequiredCommand($Name, $InstallHint) {
    $Command = Get-Command $Name -ErrorAction SilentlyContinue
    if (!$Command) {
        throw "$Name was not found. $InstallHint"
    }
    return $Command.Source
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
    throw "Python was not found. Install Python 3 with Pillow or make python.exe available on PATH."
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

function Assert-RequiredFile($Path, $Message) {
    if (!(Test-Path $Path)) {
        throw "$Message Missing file: $Path"
    }
}

Write-Step "Generating icons"
$Python = @(Find-Python)
$PythonArgs = @()
if ($Python.Length -gt 1) {
    $PythonArgs += $Python[1..($Python.Length - 1)]
}
$PythonArgs += (Join-Path $Root "installer\scripts\generate-icons.py")
Invoke-Native $Python[0] $PythonArgs "Icon generation failed."

Write-Step "Building bundled SKKOA compiler"
New-Item -ItemType Directory -Force -Path (Split-Path $CompilerOut) | Out-Null
$Sources = @(
    (Join-Path $RepoRoot "compiler\src\main.cpp"),
    (Join-Path $RepoRoot "compiler\src\ErrorReporter.cpp"),
    (Join-Path $RepoRoot "compiler\src\Lexer.cpp"),
    (Join-Path $RepoRoot "compiler\src\Parser.cpp"),
    (Join-Path $RepoRoot "compiler\src\SemanticAnalyzer.cpp"),
    (Join-Path $RepoRoot "compiler\src\CodeGenerator.cpp")
)
$Gxx = Find-RequiredCommand "g++" "Install MinGW-w64/MSYS2 or make g++.exe available on PATH."
foreach ($Source in $Sources) {
    Assert-RequiredFile $Source "Bundled compiler source file was not found."
}
Invoke-Native $Gxx (@("-std=c++17", "-O2", "-Wall", "-Wextra", "-pedantic") + $Sources + @("-I", (Join-Path $RepoRoot "compiler\src"), "-o", $CompilerOut)) "Bundled compiler build failed."

Write-Step "Copying standard modules and toolchain script"
New-Item -ItemType Directory -Force -Path (Join-Path $EditorRoot "tools\skkoa\lib") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $EditorRoot "tools\skkoa\download") | Out-Null
Assert-RequiredFile (Join-Path $RepoRoot "compiler\download\skkoa-windows.ps1") "Bundled toolchain script was not found."
Copy-Item -Path (Join-Path $RepoRoot "compiler\lib\*.koa") -Destination (Join-Path $EditorRoot "tools\skkoa\lib") -Force
Copy-Item -LiteralPath (Join-Path $RepoRoot "compiler\download\skkoa-windows.ps1") -Destination (Join-Path $EditorRoot "tools\skkoa\download\skkoa-windows.ps1") -Force

$Dotnet = Find-RequiredCommand "dotnet" "Install the .NET 8 SDK."
$Solution = Join-Path $EditorRoot "SkkoaStudio.sln"
Assert-RequiredFile $Solution "Editor solution was not found."

Write-Step "Restoring editor solution"
Invoke-Native $Dotnet @("restore", $Solution) "dotnet restore failed."

if (!$SkipTests) {
    Write-Step "Running editor tests"
    Invoke-Native $Dotnet @("test", $Solution, "-c", $Configuration) "Editor tests failed."
}

Write-Step "Publishing editor"
if (Test-Path $PublishDir) {
    Remove-Item -LiteralPath $PublishDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $ArtifactsRoot | Out-Null
$Project = Join-Path $EditorRoot "src\SkkoaStudio\SkkoaStudio.csproj"
Assert-RequiredFile $Project "Editor project was not found."
Invoke-Native $Dotnet @("publish", $Project, "-c", $Configuration, "-r", $Runtime, "--self-contained", "false", "-o", $PublishDir) "dotnet publish failed."

foreach ($Required in @(
    (Join-Path $PublishDir "SkkoaStudio.exe"),
    (Join-Path $PublishDir "assets\icons\skkoa.ico"),
    (Join-Path $PublishDir "assets\icons\skkoa-file.ico"),
    (Join-Path $PublishDir "tools\skkoa\skkoa.exe"),
    (Join-Path $PublishDir "tools\skkoa\lib\stack.koa"),
    (Join-Path $PublishDir "tools\skkoa\lib\queue.koa"),
    (Join-Path $PublishDir "tools\skkoa\lib\structures.koa")
)) {
    Assert-RequiredFile $Required "Required publish file missing."
}

Write-Step "Creating zip artifact"
if (Test-Path $ZipPath) {
    Remove-Item -LiteralPath $ZipPath -Force
}
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force

Write-Step "Editor artifact: $PublishDir"
Write-Step "Editor zip: $ZipPath"
