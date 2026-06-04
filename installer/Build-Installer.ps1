param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipPublish,
    [switch]$Sign,
    [string]$CertificatePath = $env:AITEBAR_SIGN_CERT_PATH,
    [string]$CertificatePassword = $env:AITEBAR_SIGN_CERT_PASSWORD,
    [string]$TimestampUrl = $(if ($env:AITEBAR_SIGN_TIMESTAMP_URL) { $env:AITEBAR_SIGN_TIMESTAMP_URL } else { "http://timestamp.digicert.com" })
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "AiteBar\AiteBar.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$issPath = Join-Path $PSScriptRoot "AiteBar.iss"
$projectXml = [xml](Get-Content $projectPath)
$appVersion = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($appVersion)) {
    throw "Version not found in $projectPath"
}

if (-not $SkipPublish) {
    if (Test-Path $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

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

# Cleanup any temporary files left by previous Inno Setup runs (before)
Get-ChildItem -Path $installerDir -Filter "*.tmp" -Force -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

& $iscc "/Qp" "/DAppVersion=$appVersion" $issPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE"
}

# Cleanup any temporary files left by Inno Setup (after)
Get-ChildItem -Path $installerDir -Filter "*.tmp" -Force -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

$installers = Get-ChildItem -Path $installerDir -Filter "*.exe" -File
if ($installers.Count -ne 1) {
    throw "Expected exactly one installer artifact, found $($installers.Count)."
}

$installerVersion = (Get-Item $installers[0].FullName).VersionInfo.ProductVersion
$installerVersion = if ($installerVersion) { $installerVersion.Trim() } else { "" }
if ($installerVersion -and $installerVersion -ne $appVersion) {
    throw "Installer ProductVersion $installerVersion does not match app version $appVersion."
}

if ($Sign) {
    if ([string]::IsNullOrWhiteSpace($CertificatePath) -or -not (Test-Path $CertificatePath)) {
        throw "Code signing requested, but certificate path is missing or not found."
    }

    if ([string]::IsNullOrWhiteSpace($CertificatePassword)) {
        throw "Code signing requested, but certificate password is missing."
    }

    $signtoolCandidates = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\x64\signtool.exe",
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe",
        "$env:ProgramFiles\Windows Kits\10\bin\x64\signtool.exe",
        "$env:ProgramFiles\Windows Kits\10\bin\*\x64\signtool.exe"
    )

    $signtool = $signtoolCandidates |
        Get-ChildItem -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -ExpandProperty FullName -First 1

    if (-not $signtool) {
        throw "signtool.exe not found. Install the Windows SDK or run on a Windows runner with signing tools."
    }

    & $signtool sign `
        /f $CertificatePath `
        /p $CertificatePassword `
        /fd SHA256 `
        /tr $TimestampUrl `
        /td SHA256 `
        $installers[0].FullName

    if ($LASTEXITCODE -ne 0) {
        throw "signtool.exe failed with exit code $LASTEXITCODE"
    }

    $signature = Get-AuthenticodeSignature -FilePath $installers[0].FullName
    if ($signature.Status -ne "Valid") {
        throw "Installer signature is not valid. Status: $($signature.Status)"
    }

    Write-Host "Installer signature verified: $($signature.SignerCertificate.Subject)"
} else {
    Write-Host "Code signing skipped. Pass -Sign with a PFX certificate to sign the installer."
}

Write-Host "Installer created in $installerDir"
