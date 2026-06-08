$ErrorActionPreference = "Stop"

$BaseUrl = if ($env:SKKOA_BASE_URL) { $env:SKKOA_BASE_URL } else { "https://skkoa.toyotech.dev/compiler" }
$InstallRoot = if ($env:SKKOA_INSTALL_ROOT) { $env:SKKOA_INSTALL_ROOT } else { Join-Path $env:LOCALAPPDATA "SKKOA" }
$SourceDir = Join-Path $InstallRoot "source"
$BuildDir = Join-Path $InstallRoot "build"
$BinDir = if ($env:SKKOA_BIN_DIR) { $env:SKKOA_BIN_DIR } else { Join-Path $env:LOCALAPPDATA "Programs\SKKOA\bin" }

$Files = @(
    "CMakeLists.txt",
    "src/Ast.hpp",
    "src/CodeGenerator.cpp",
    "src/CodeGenerator.hpp",
    "src/ErrorReporter.cpp",
    "src/ErrorReporter.hpp",
    "src/Lexer.cpp",
    "src/Lexer.hpp",
    "src/main.cpp",
    "src/Parser.cpp",
    "src/Parser.hpp",
    "src/SemanticAnalyzer.cpp",
    "src/SemanticAnalyzer.hpp",
    "src/Token.hpp"
)

function Write-Skkoa($Message) {
    Write-Host "[skkoa] $Message"
}

function Get-CxxCompiler {
    if ($env:CXX) {
        $candidate = Get-Command $env:CXX -ErrorAction SilentlyContinue
        if ($candidate) { return $candidate.Source }
    }

    foreach ($name in @("g++", "clang++", "cl")) {
        $candidate = Get-Command $name -ErrorAction SilentlyContinue
        if ($candidate) { return $candidate.Source }
    }

    throw "C++17 compiler not found. Install MSYS2 g++, LLVM clang++, or run from a Visual Studio Developer Prompt."
}

function Download-Source {
    if (Test-Path $SourceDir) {
        Remove-Item -LiteralPath $SourceDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $SourceDir "src") | Out-Null

    foreach ($path in $Files) {
        $url = "$BaseUrl/$path"
        $dest = Join-Path $SourceDir $path
        New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
        Invoke-WebRequest -UseBasicParsing $url -OutFile $dest
    }
}

function Build-Compiler {
    $compiler = Get-CxxCompiler
    if (Test-Path $BuildDir) {
        Remove-Item -LiteralPath $BuildDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null

    $sources = @(
        (Join-Path $SourceDir "src/main.cpp")
        (Join-Path $SourceDir "src/ErrorReporter.cpp")
        (Join-Path $SourceDir "src/Lexer.cpp")
        (Join-Path $SourceDir "src/Parser.cpp")
        (Join-Path $SourceDir "src/SemanticAnalyzer.cpp")
        (Join-Path $SourceDir "src/CodeGenerator.cpp")
    )
    $output = Join-Path $BuildDir "skkoa.exe"
    $include = Join-Path $SourceDir "src"

    if ((Split-Path $compiler -Leaf).ToLowerInvariant() -eq "cl.exe") {
        & $compiler /nologo /EHsc /std:c++17 /O2 /I $include $sources /Fe:$output
    } else {
        & $compiler -std=c++17 -O2 -Wall -Wextra -pedantic $sources -I $include -o $output
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Compiler build failed."
    }
    if (!(Test-Path $output)) {
        throw "Build succeeded but skkoa.exe was not created."
    }
}

function Install-Command {
    New-Item -ItemType Directory -Force -Path $BinDir | Out-Null
    Copy-Item -LiteralPath (Join-Path $BuildDir "skkoa.exe") -Destination (Join-Path $BinDir "skkoa.exe") -Force

    $launcher = Join-Path $BinDir "skkoa.cmd"
    @"
@echo off
"$BinDir\skkoa.exe" %*
"@ | Set-Content -Path $launcher -Encoding ASCII

    $windowsApps = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps"
    if (!$env:SKKOA_SKIP_PATH_UPDATE -and (Test-Path $windowsApps)) {
        $windowsAppsLauncher = Join-Path $windowsApps "skkoa.cmd"
        @"
@echo off
"$BinDir\skkoa.exe" %*
"@ | Set-Content -Path $windowsAppsLauncher -Encoding ASCII
    }

    if (!$env:SKKOA_SKIP_PATH_UPDATE) {
        $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
        if (!$userPath) { $userPath = "" }
        $pathParts = $userPath -split ";" | Where-Object { $_ -ne "" }
        if ($pathParts -notcontains $BinDir) {
            [Environment]::SetEnvironmentVariable("Path", ($pathParts + $BinDir) -join ";", "User")
        }
    }
}

function Warn-Tools {
    $missing = @()
    foreach ($tool in @("nasm", "gcc")) {
        if (!(Get-Command $tool -ErrorAction SilentlyContinue)) {
            $missing += $tool
        }
    }
    if ($missing.Count -gt 0) {
        Write-Warning "Missing runtime tool(s): $($missing -join ', '). SKKOA uses NASM and GCC when it links a .koa file into an executable."
    }
}

Write-Skkoa "Installing SKKOA compiler for Windows"
Download-Source
Build-Compiler
Install-Command
Warn-Tools
Write-Skkoa "Installed: $BinDir\skkoa.exe"
Write-Skkoa "Open a new terminal and run: skkoa hello.koa"
