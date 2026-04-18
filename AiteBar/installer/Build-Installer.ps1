param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "AiteBar\AiteBar.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$issPath = Join-Path $PSScriptRoot "AiteBar.iss"

if (-not $SkipPublish) {
    dotnet publish $projectPath `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:PublishReadyToRun=false `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}

if (-not (Test-Path $publishDir)) {
    throw "Publish output not found: $publishDir"
}

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 (ISCC.exe) not found. Install Inno Setup and rerun the script."
}

New-Item -ItemType Directory -Force -Path $installerDir | Out-Null

& $iscc "/Qp" $issPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE"
}

# Cleanup temporary files left by Inno Setup
Get-ChildItem -Path $installerDir -Filter "*.tmp" -Force | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "Installer created in $installerDir"
