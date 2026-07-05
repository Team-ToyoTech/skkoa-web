param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $Root
$InstallerOutput = Join-Path $Root "installer\output\SKKOA-Studio-Setup-x64.exe"
$WebStudioDownload = Join-Path $RepoRoot "download\studio\SKKOA-Studio-Setup-x64.exe"

& (Join-Path $Root "build-editor.ps1") -Configuration $Configuration -Runtime $Runtime -SkipTests:$SkipTests
& (Join-Path $Root "build-installer.ps1") -Configuration $Configuration

if (!(Test-Path $InstallerOutput)) {
    throw "Installer output missing: $InstallerOutput"
}
New-Item -ItemType Directory -Force -Path (Split-Path $WebStudioDownload) | Out-Null
Copy-Item -LiteralPath $InstallerOutput -Destination $WebStudioDownload -Force

Write-Host ""
Write-Host "Artifacts:"
Write-Host "  Editor:    $(Join-Path $Root 'editor\artifacts\SKKOA-Studio-win-x64')"
Write-Host "  Zip:       $(Join-Path $Root 'editor\artifacts\SKKOA-Studio-win-x64.zip')"
Write-Host "  Installer: $(Join-Path $Root 'installer\output\SKKOA-Studio-Setup-x64.exe')"
Write-Host "  Web EXE:   $WebStudioDownload"
