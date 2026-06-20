[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Test,

    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

Write-Host "=== AiteBar Safe Build ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray

$buildArgs = @(".\AiteBar.sln", "-c", $Configuration, "-m:1", "-p:UseSharedCompilation=false")
if ($NoRestore) {
    $buildArgs += "--no-restore"
}

Write-Host "[1/2] Building solution with serialized MSBuild and disabled shared compilation..." -ForegroundColor Yellow
& dotnet build @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

if ($Test) {
    Write-Host "[2/2] Running tests from the built DLL..." -ForegroundColor Yellow
    $testDll = ".\AiteBar.Tests\bin\$Configuration\net10.0-windows\AiteBar.Tests.dll"
    if (-not (Test-Path -LiteralPath $testDll)) {
        throw "Test assembly was not found: $testDll"
    }

    & dotnet vstest $testDll
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet vstest failed with exit code $LASTEXITCODE."
    }
} else {
    Write-Host "[2/2] Tests skipped. Pass -Test to run them." -ForegroundColor DarkYellow
}

Write-Host "=== SAFE BUILD SUCCEEDED ===" -ForegroundColor Green
