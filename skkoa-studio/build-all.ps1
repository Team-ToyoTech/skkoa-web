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
$UpdateArtifact = Join-Path $Root "editor\artifacts\updates\$Runtime"
$WebUpdateDir = Join-Path $RepoRoot "download\studio\updates\$Runtime"

& (Join-Path $Root "build-editor.ps1") -Configuration $Configuration -Runtime $Runtime -SkipTests:$SkipTests
& (Join-Path $Root "build-installer.ps1") -Configuration $Configuration

if (!(Test-Path $InstallerOutput)) {
    throw "Installer output missing: $InstallerOutput"
}
New-Item -ItemType Directory -Force -Path (Split-Path $WebStudioDownload) | Out-Null
Copy-Item -LiteralPath $InstallerOutput -Destination $WebStudioDownload -Force

if (!(Test-Path (Join-Path $UpdateArtifact "manifest.json"))) {
    throw "Update manifest missing: $(Join-Path $UpdateArtifact 'manifest.json')"
}
if (Test-Path $WebUpdateDir) {
    $ResolvedWebUpdateDir = [System.IO.Path]::GetFullPath($WebUpdateDir)
    $ExpectedRoot = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot "download\studio\updates"))
    $ExpectedRootWithSeparator = $ExpectedRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (!$ResolvedWebUpdateDir.StartsWith($ExpectedRootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset unexpected update directory: $ResolvedWebUpdateDir"
    }
    Remove-Item -LiteralPath $ResolvedWebUpdateDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $WebUpdateDir | Out-Null
Copy-Item -Path (Join-Path $UpdateArtifact "*") -Destination $WebUpdateDir -Recurse -Force

Write-Host ""
Write-Host "Artifacts:"
Write-Host "  Editor:    $(Join-Path $Root 'editor\artifacts\SKKOA-Studio-win-x64')"
Write-Host "  Zip:       $(Join-Path $Root 'editor\artifacts\SKKOA-Studio-win-x64.zip')"
Write-Host "  Installer: $(Join-Path $Root 'installer\output\SKKOA-Studio-Setup-x64.exe')"
Write-Host "  Web EXE:   $WebStudioDownload"
Write-Host "  Updates:   $WebUpdateDir"
