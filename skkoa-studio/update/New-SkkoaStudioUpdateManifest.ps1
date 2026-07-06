param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDir,
    [Parameter(Mandatory = $true)]
    [string]$OutputDir,
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Runtime = "win-x64",
    [string]$FilesBaseUrl = "files/",
    [string]$InstallerUrl = "/download/studio/SKKOA-Studio-Setup-x64.exe",
    [string]$ReleaseNotes = "",
    [string]$DeleteListPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Reset-Directory($Path) {
    $FullPath = [System.IO.Path]::GetFullPath($Path)
    $RootPath = [System.IO.Path]::GetPathRoot($FullPath)
    if ($FullPath.TrimEnd('\', '/') -eq $RootPath.TrimEnd('\', '/')) {
        throw "Refusing to reset a filesystem root: $FullPath"
    }

    if (Test-Path $FullPath) {
        Remove-Item -LiteralPath $FullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $FullPath | Out-Null
    return $FullPath
}

function Get-RelativePath($Root, $Path) {
    $RootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $PathFull = [System.IO.Path]::GetFullPath($Path)
    if (!$PathFull.StartsWith($RootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside root. Root: $RootFull Path: $PathFull"
    }
    return $PathFull.Substring($RootFull.Length).TrimStart('\', '/').Replace('\', '/')
}

function ConvertTo-UpdatePath($Path) {
    return $Path.Replace('\', '/').Trim('/')
}

function Read-DeleteList($Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or !(Test-Path $Path)) {
        return @()
    }

    return Get-Content -LiteralPath $Path |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -and !$_.StartsWith("#") } |
        ForEach-Object { ConvertTo-UpdatePath $_ }
}

$SourceRoot = [System.IO.Path]::GetFullPath($SourceDir)
if (!(Test-Path $SourceRoot)) {
    throw "Source directory was not found: $SourceRoot"
}

$OutputRoot = Reset-Directory $OutputDir
$FilesRoot = Join-Path $OutputRoot "files"
New-Item -ItemType Directory -Force -Path $FilesRoot | Out-Null

$ManifestFiles = @()
$SourceFiles = Get-ChildItem -LiteralPath $SourceRoot -Recurse -File |
    Sort-Object FullName |
    Where-Object {
        $Relative = Get-RelativePath $SourceRoot $_.FullName
        !$Relative.StartsWith("updates/", [System.StringComparison]::OrdinalIgnoreCase)
    }

foreach ($File in $SourceFiles) {
    $RelativePath = Get-RelativePath $SourceRoot $File.FullName
    $TargetPath = Join-Path $FilesRoot ($RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    New-Item -ItemType Directory -Force -Path (Split-Path $TargetPath) | Out-Null
    Copy-Item -LiteralPath $File.FullName -Destination $TargetPath -Force

    $Hash = (Get-FileHash -LiteralPath $File.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $ManifestFiles += [ordered]@{
        path = $RelativePath
        size = $File.Length
        sha256 = $Hash
        url = $RelativePath
    }
}

$Manifest = [ordered]@{
    schemaVersion = 1
    application = "SKKOA Studio"
    version = $Version
    runtime = $Runtime
    publishedAt = [System.DateTimeOffset]::UtcNow.ToString("O")
    minimumUpdaterVersion = "0.1.0"
    filesBaseUrl = $FilesBaseUrl
    installerUrl = $InstallerUrl
    releaseNotes = $ReleaseNotes
    files = $ManifestFiles
    delete = @(Read-DeleteList $DeleteListPath)
}

$ManifestJson = $Manifest | ConvertTo-Json -Depth 8
$ManifestPath = Join-Path $OutputRoot "manifest.json"
$ManifestJson | Set-Content -LiteralPath $ManifestPath -Encoding UTF8

$InstalledManifestDir = Join-Path $SourceRoot "updates"
New-Item -ItemType Directory -Force -Path $InstalledManifestDir | Out-Null
$ManifestJson | Set-Content -LiteralPath (Join-Path $InstalledManifestDir "current-manifest.json") -Encoding UTF8

Write-Host "Update manifest: $ManifestPath"
Write-Host "Update files:    $FilesRoot"
