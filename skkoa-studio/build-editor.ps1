param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipTests,
    [switch]$SkipBundledToolchain
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $Root
$EditorRoot = Join-Path $Root "editor"
$ArtifactsRoot = Join-Path $EditorRoot "artifacts"
$PublishDir = Join-Path $ArtifactsRoot "SKKOA-Studio-win-x64"
$ZipPath = Join-Path $ArtifactsRoot "SKKOA-Studio-win-x64.zip"
$UpdateArtifactsDir = Join-Path $ArtifactsRoot "updates\$Runtime"
$CompilerOut = Join-Path $EditorRoot "tools\skkoa\skkoa.exe"
$BundledToolchainRoot = Join-Path $EditorRoot "tools\skkoa\toolchain"
$BundledMsysRoot = Join-Path $BundledToolchainRoot "msys64"
$BundledMingwBin = Join-Path $BundledMsysRoot "mingw64\bin"
$BundledUsrBin = Join-Path $BundledMsysRoot "usr\bin"

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

function Add-BundledToolchainPath {
    $Paths = @($BundledMingwBin, $BundledUsrBin) + ($env:Path -split ";")
    $env:Path = ($Paths | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique) -join ";"
}

function Test-MsysToolchain($MsysRoot) {
    return (
        (Test-Path (Join-Path $MsysRoot "mingw64\bin\gcc.exe")) -and
        (Test-Path (Join-Path $MsysRoot "mingw64\bin\g++.exe")) -and
        (Test-Path (Join-Path $MsysRoot "mingw64\bin\nasm.exe")) -and
        (Test-Path (Join-Path $MsysRoot "usr\bin\bash.exe"))
    )
}

function Find-ExistingMsysRoot {
    $Candidates = @()
    foreach ($Tool in @("g++", "gcc", "nasm")) {
        $Command = Get-Command $Tool -ErrorAction SilentlyContinue
        if ($Command) {
            $ToolPath = Split-Path -Parent $Command.Source
            $MingwRoot = Split-Path -Parent $ToolPath
            $Candidate = Split-Path -Parent $MingwRoot
            $Candidates += $Candidate
        }
    }
    $Candidates += $env:MSYS2_LOCATION
    $Candidates += "C:\msys64"
    $Candidates += "C:\tools\msys64"
    if ($env:LOCALAPPDATA) {
        $Candidates += (Join-Path $env:LOCALAPPDATA "Programs\msys64")
    }

    foreach ($Candidate in $Candidates | Where-Object { $_ } | Select-Object -Unique) {
        try {
            if (Test-MsysToolchain $Candidate) {
                return (Resolve-Path $Candidate).Path
            }
        }
        catch {
        }
    }
    return $null
}

function Invoke-RobocopyMirror($Source, $Destination) {
    New-Item -ItemType Directory -Force -Path (Split-Path $Destination) | Out-Null
    & robocopy $Source $Destination /MIR /R:2 /W:2 /NFL /NDL /NJH /NJS /NP | Out-Host
    if ($LASTEXITCODE -gt 7) {
        throw "Toolchain copy failed. robocopy exit code: $LASTEXITCODE"
    }
    $global:LASTEXITCODE = 0
}

function Remove-ToolchainCaches {
    foreach ($Path in @(
        (Join-Path $BundledMsysRoot "var\cache\pacman\pkg"),
        (Join-Path $BundledMsysRoot "tmp")
    )) {
        if (Test-Path $Path) {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Install-MsysToolchainIntoBundle {
    Write-Step "Downloading MSYS2 toolchain for bundled NASM/GCC"
    New-Item -ItemType Directory -Force -Path $BundledToolchainRoot | Out-Null
    $Headers = @{ "User-Agent" = "SKKOA Studio release build" }
    $Release = Invoke-RestMethod -Headers $Headers "https://api.github.com/repos/msys2/msys2-installer/releases/latest"
    $Asset = $Release.assets |
        Where-Object { $_.name -match "^msys2-base-x86_64-.*\.sfx\.exe$" } |
        Select-Object -First 1
    if (!$Asset) {
        throw "Could not find an MSYS2 self-extracting installer asset."
    }

    $TempRoot = if ($env:TEMP) { $env:TEMP } else { [System.IO.Path]::GetTempPath() }
    $InstallerPath = Join-Path $TempRoot $Asset.name
    $PartialPath = "$InstallerPath.partial"
    Remove-Item -LiteralPath $PartialPath -Force -ErrorAction SilentlyContinue
    Invoke-WebRequest -UseBasicParsing -Uri $Asset.browser_download_url -OutFile $PartialPath
    Move-Item -LiteralPath $PartialPath -Destination $InstallerPath -Force
    & $InstallerPath -y "-o$BundledToolchainRoot"
    if ($LASTEXITCODE -ne 0) {
        throw "MSYS2 extraction failed. Exit code: $LASTEXITCODE"
    }
    Remove-Item -LiteralPath $InstallerPath -Force -ErrorAction SilentlyContinue

    Add-BundledToolchainPath
    $Bash = Join-Path $BundledUsrBin "bash.exe"
    Assert-RequiredFile $Bash "MSYS2 bash was not found after extraction."
    & $Bash -lc "pacman --noconfirm -Syu || true"
    & $Bash -lc "pacman --noconfirm --needed -S mingw-w64-x86_64-gcc mingw-w64-x86_64-nasm"
    if ($LASTEXITCODE -ne 0) {
        throw "MSYS2 package installation failed. Exit code: $LASTEXITCODE"
    }
}

function Ensure-BundledToolchain {
    if ($SkipBundledToolchain) {
        Write-Step "Skipping bundled NASM/GCC toolchain staging"
        return
    }

    if (Test-MsysToolchain $BundledMsysRoot) {
        Write-Step "Using existing bundled NASM/GCC toolchain"
        Add-BundledToolchainPath
        return
    }

    $ExistingMsysRoot = Find-ExistingMsysRoot
    if ($ExistingMsysRoot) {
        Write-Step "Copying MSYS2 NASM/GCC toolchain from $ExistingMsysRoot"
        Invoke-RobocopyMirror $ExistingMsysRoot $BundledMsysRoot
    }
    else {
        Install-MsysToolchainIntoBundle
    }

    Remove-ToolchainCaches
    if (!(Test-MsysToolchain $BundledMsysRoot)) {
        throw "Bundled toolchain is incomplete. Expected gcc.exe, g++.exe, nasm.exe, and bash.exe under $BundledMsysRoot."
    }
    Add-BundledToolchainPath
}

function Write-CompilerLauncher {
    $Launcher = Join-Path $EditorRoot "tools\skkoa\skkoa.cmd"
    @"
@echo off
set "SKKOA_HOME=%~dp0"
set "PATH=%SKKOA_HOME%toolchain\msys64\mingw64\bin;%SKKOA_HOME%toolchain\msys64\usr\bin;%PATH%"
"%SKKOA_HOME%skkoa.exe" %*
"@ | Set-Content -Path $Launcher -Encoding ASCII
}

Write-Step "Generating icons"
$Python = @(Find-Python)
$PythonArgs = @()
if ($Python.Length -gt 1) {
    $PythonArgs += $Python[1..($Python.Length - 1)]
}
$PythonArgs += (Join-Path $Root "installer\scripts\generate-icons.py")
Invoke-Native $Python[0] $PythonArgs "Icon generation failed."

Ensure-BundledToolchain

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
Write-CompilerLauncher

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

Write-Step "Publishing updater"
$UpdaterProject = Join-Path $EditorRoot "src\SkkoaStudio.Updater\SkkoaStudio.Updater.csproj"
Assert-RequiredFile $UpdaterProject "Updater project was not found."
Invoke-Native $Dotnet @("publish", $UpdaterProject, "-c", $Configuration, "-r", $Runtime, "--self-contained", "false", "-o", $PublishDir) "dotnet updater publish failed."

$RequiredFiles = @(
    (Join-Path $PublishDir "SkkoaStudio.exe"),
    (Join-Path $PublishDir "SkkoaStudio.Updater.exe"),
    (Join-Path $PublishDir "SkkoaStudio.Updater.deps.json"),
    (Join-Path $PublishDir "SkkoaStudio.Updater.runtimeconfig.json"),
    (Join-Path $PublishDir "assets\icons\skkoa.ico"),
    (Join-Path $PublishDir "assets\icons\skkoa-file.ico"),
    (Join-Path $PublishDir "tools\skkoa\skkoa.exe"),
    (Join-Path $PublishDir "tools\skkoa\skkoa.cmd"),
    (Join-Path $PublishDir "tools\skkoa\lib\stack.koa"),
    (Join-Path $PublishDir "tools\skkoa\lib\queue.koa"),
    (Join-Path $PublishDir "tools\skkoa\lib\structures.koa")
)
if (!$SkipBundledToolchain) {
    $RequiredFiles += @(
        (Join-Path $PublishDir "tools\skkoa\toolchain\msys64\mingw64\bin\gcc.exe"),
        (Join-Path $PublishDir "tools\skkoa\toolchain\msys64\mingw64\bin\g++.exe"),
        (Join-Path $PublishDir "tools\skkoa\toolchain\msys64\mingw64\bin\nasm.exe"),
        (Join-Path $PublishDir "tools\skkoa\toolchain\msys64\usr\bin\bash.exe")
    )
}
foreach ($Required in $RequiredFiles) {
    Assert-RequiredFile $Required "Required publish file missing."
}

Write-Step "Generating update manifest"
[xml]$ProjectXml = Get-Content -LiteralPath $Project
$AppVersion = ($ProjectXml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if ([string]::IsNullOrWhiteSpace($AppVersion)) {
    throw "Could not read editor version from $Project"
}
$UpdateManifestScript = Join-Path $Root "update\New-SkkoaStudioUpdateManifest.ps1"
Assert-RequiredFile $UpdateManifestScript "Update manifest script was not found."
& $UpdateManifestScript -SourceDir $PublishDir -OutputDir $UpdateArtifactsDir -Version $AppVersion -Runtime $Runtime

Write-Step "Creating zip artifact"
if (Test-Path $ZipPath) {
    Remove-Item -LiteralPath $ZipPath -Force
}
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force

Write-Step "Editor artifact: $PublishDir"
Write-Step "Editor zip: $ZipPath"
Write-Step "Update manifest: $(Join-Path $UpdateArtifactsDir 'manifest.json')"
