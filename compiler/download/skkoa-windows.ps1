$ErrorActionPreference = "Stop"

$BaseUrl = if ($env:SKKOA_BASE_URL) { $env:SKKOA_BASE_URL } else { "https://skkoa.toyotech.dev/compiler" }
$InstallRoot = if ($env:SKKOA_INSTALL_ROOT) { $env:SKKOA_INSTALL_ROOT } else { Join-Path $env:LOCALAPPDATA "SKKOA" }
$SourceDir = Join-Path $InstallRoot "source"
$BuildDir = Join-Path $InstallRoot "build"
$LibDir = Join-Path $InstallRoot "lib"
$BinDir = if ($env:SKKOA_BIN_DIR) { $env:SKKOA_BIN_DIR } else { Join-Path $env:LOCALAPPDATA "Programs\SKKOA\bin" }
$ToolchainRoot = if ($env:SKKOA_TOOLCHAIN_ROOT) { $env:SKKOA_TOOLCHAIN_ROOT } else { Join-Path $InstallRoot "toolchain" }
$MsysRoot = Join-Path $ToolchainRoot "msys64"
$MingwBin = Join-Path $MsysRoot "mingw64\bin"
$UsrBin = Join-Path $MsysRoot "usr\bin"

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
    "src/Token.hpp",
    "lib/stack.koa",
    "lib/queue.koa",
    "lib/structures.koa"
)

function Write-Skkoa($Message) {
    Write-Host "[skkoa] $Message"
}

function Add-ToolchainPath {
    $paths = @($MingwBin, $UsrBin) + ($env:Path -split ";")
    $env:Path = ($paths | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique) -join ";"
}

function Install-Toolchain {
    if ($env:SKKOA_SKIP_TOOLCHAIN_INSTALL) {
        Add-ToolchainPath
        return
    }

    $gcc = Join-Path $MingwBin "g++.exe"
    $nasm = Join-Path $MingwBin "nasm.exe"
    if ((Test-Path $gcc) -and (Test-Path $nasm)) {
        Add-ToolchainPath
        return
    }

    Write-Skkoa "Installing bundled MSYS2 toolchain"
    New-Item -ItemType Directory -Force -Path $ToolchainRoot | Out-Null
    $bash = Join-Path $UsrBin "bash.exe"
    if (!(Test-Path $bash)) {
        $headers = @{ "User-Agent" = "SKKOA installer" }
        $release = Invoke-RestMethod -Headers $headers "https://api.github.com/repos/msys2/msys2-installer/releases/latest"
        $asset = $release.assets |
            Where-Object { $_.name -match "^msys2-base-x86_64-.*\.sfx\.exe$" } |
            Select-Object -First 1
        if (!$asset) {
            throw "Could not find an MSYS2 self-extracting installer asset."
        }
        $installer = Join-Path $env:TEMP $asset.name
        Invoke-WebRequest -UseBasicParsing $asset.browser_download_url -OutFile $installer
        & $installer -y "-o$ToolchainRoot"
        if ($LASTEXITCODE -ne 0) {
            throw "MSYS2 extraction failed."
        }
        Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
    }

    Add-ToolchainPath
    $bash = Join-Path $UsrBin "bash.exe"
    if (!(Test-Path $bash)) {
        throw "MSYS2 bash was not found after extraction."
    }

    & $bash -lc "pacman --noconfirm -Syu || true"
    & $bash -lc "pacman --noconfirm --needed -S mingw-w64-x86_64-gcc mingw-w64-x86_64-nasm"
    if ($LASTEXITCODE -ne 0) {
        throw "MSYS2 package installation failed."
    }
    Add-ToolchainPath
}

function Get-CxxCompiler {
    Install-Toolchain
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

function Install-Libraries {
    if (Test-Path $LibDir) {
        Remove-Item -LiteralPath $LibDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $LibDir | Out-Null
    $sourceLib = Join-Path $SourceDir "lib"
    if (Test-Path $sourceLib) {
        Copy-Item -Path (Join-Path $sourceLib "*") -Destination $LibDir -Recurse -Force
    }
}

function Install-Command {
    New-Item -ItemType Directory -Force -Path $BinDir | Out-Null
    Copy-Item -LiteralPath (Join-Path $BuildDir "skkoa.exe") -Destination (Join-Path $BinDir "skkoa.exe") -Force

    $launcher = Join-Path $BinDir "skkoa.cmd"
    @"
@echo off
set "PATH=$MingwBin;$UsrBin;%PATH%"
"$BinDir\skkoa.exe" %*
"@ | Set-Content -Path $launcher -Encoding ASCII

    $windowsApps = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps"
    if (!$env:SKKOA_SKIP_PATH_UPDATE -and (Test-Path $windowsApps)) {
        $windowsAppsLauncher = Join-Path $windowsApps "skkoa.cmd"
        @"
@echo off
set "PATH=$MingwBin;$UsrBin;%PATH%"
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
    Add-ToolchainPath
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
Install-Toolchain
Download-Source
Install-Libraries
Build-Compiler
Install-Command
Warn-Tools
Write-Skkoa "Installed: $BinDir\skkoa.exe"
Write-Skkoa "Open a new terminal and run: skkoa hello.koa"
