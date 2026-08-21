[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Test,

    [switch]$NoRestore,

    [switch]$Clean,

    [switch]$Installer
)

$ErrorActionPreference = "Stop"

Write-Host "=== AiteBar Safe Build ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray

if ($Clean) {
    Write-Host "[0/3] Stopping build servers and removing generated outputs..." -ForegroundColor Yellow
    & dotnet build-server shutdown

    $generatedPaths = @(
        ".\AiteBar\bin",
        ".\AiteBar\obj",
        ".\AiteBar.Tests\bin",
        ".\AiteBar.Tests\obj",
        ".\artifacts\publish",
        ".\artifacts\installer"
    )

    foreach ($path in $generatedPaths) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

$buildArgs = @(".\AiteBar.sln", "-c", $Configuration, "-m:1", "-p:UseSharedCompilation=false")
if ($NoRestore) {
    $buildArgs += "--no-restore"
}

Write-Host "[1/3] Building solution with serialized MSBuild and disabled shared compilation..." -ForegroundColor Yellow
& dotnet build @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

if ($Test) {
    Write-Host "[2/3] Running tests from the built DLL..." -ForegroundColor Yellow
    $testDll = ".\AiteBar.Tests\bin\$Configuration\net10.0-windows\AiteBar.Tests.dll"
    if (-not (Test-Path -LiteralPath $testDll)) {
        throw "Test assembly was not found: $testDll"
    }

    & dotnet vstest $testDll
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet vstest failed with exit code $LASTEXITCODE."
    }
} else {
    Write-Host "[2/3] Tests skipped. Pass -Test to run them." -ForegroundColor DarkYellow
}

if ($Installer) {
    if ($Configuration -ne "Release") {
        throw "Installer creation requires -Configuration Release."
    }

    Write-Host "[3/3] Building installer..." -ForegroundColor Yellow
    & .\installer\Build-Installer.ps1 -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Installer build failed with exit code $LASTEXITCODE."
    }
}

Write-Host "=== SAFE BUILD SUCCEEDED ===" -ForegroundColor Green
