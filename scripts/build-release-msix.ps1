param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$LayoutDir = Join-Path $RepoRoot "AppPackages\AppxLayout"
$ReleaseDir = Join-Path $RepoRoot "AppPackages\Release"

& (Join-Path $PSScriptRoot "package-msix.ps1") -Configuration $Configuration -Platform $Platform

$ManifestPath = Join-Path $LayoutDir "AppxManifest.xml"
[xml]$ManifestXml = Get-Content $ManifestPath
$Identity = $ManifestXml.Package.Identity
$RawVersion = $Identity.Version
$VersionParts = $RawVersion.Split(".")
$SemVer = if ($VersionParts.Length -ge 3) { "$($VersionParts[0]).$($VersionParts[1]).$($VersionParts[2])" } else { $RawVersion }
$Publisher = $Identity.Publisher

$KitsBinRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
$SdkToolDir = Get-ChildItem -Path $KitsBinRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName "x64\makeappx.exe") } |
    Sort-Object Name -Descending |
    Select-Object -First 1

if (-not $SdkToolDir) {
    throw "Windows 10/11 SDK (makeappx.exe / signtool.exe) not found under $KitsBinRoot."
}

$MakeAppx = Join-Path $SdkToolDir.FullName "x64\makeappx.exe"
$SignTool = Join-Path $SdkToolDir.FullName "x64\signtool.exe"

if (Test-Path $ReleaseDir) { Remove-Item -Recurse -Force $ReleaseDir }
New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null

$MsixName = "CustomTwitchChat.GameBar-v$SemVer-$Platform.msix"
$MsixPath = Join-Path $ReleaseDir $MsixName

Write-Host "Packing $MsixName via $MakeAppx..."
& $MakeAppx pack /o /d $LayoutDir /p $MsixPath
if ($LASTEXITCODE -ne 0) { throw "makeappx.exe failed with exit code $LASTEXITCODE" }

$UsesSelfSignedCert = $true
if (-not [string]::IsNullOrWhiteSpace($env:MSIX_PFX_BASE64)) {
    $UsesSelfSignedCert = $false
    $TempPfx = Join-Path $env:TEMP "ctc-release-sign.pfx"
    try {
        [System.IO.File]::WriteAllBytes($TempPfx, [Convert]::FromBase64String($env:MSIX_PFX_BASE64))
        $SignArgs = @("sign", "/fd", "SHA256", "/a", "/f", $TempPfx, "/tr", "http://timestamp.digicert.com", "/td", "SHA256")
        if (-not [string]::IsNullOrWhiteSpace($env:MSIX_PFX_PASSWORD)) {
            $SignArgs += @("/p", $env:MSIX_PFX_PASSWORD)
        }
        $SignArgs += $MsixPath
        & $SignTool @SignArgs
        if ($LASTEXITCODE -ne 0) { throw "signtool.exe failed with exit code $LASTEXITCODE" }
    } finally {
        if (Test-Path $TempPfx) { Remove-Item -Force $TempPfx }
    }
} else {
    Write-Host "Generating self-signed code-signing certificate for $Publisher..."
    $Cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $Publisher `
        -KeyUsage DigitalSignature `
        -FriendlyName "Custom Twitch Chat Code Signing" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
        -NotAfter (Get-Date).AddYears(3)

    try {
        $CerPath = Join-Path $ReleaseDir "CustomTwitchChat.cer"
        Export-Certificate -Cert $Cert -FilePath $CerPath | Out-Null
        & $SignTool sign /fd SHA256 /sha1 $Cert.Thumbprint $MsixPath
        if ($LASTEXITCODE -ne 0) { throw "signtool.exe failed with exit code $LASTEXITCODE" }
    } finally {
        Remove-Item -Path "Cert:\CurrentUser\My\$($Cert.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }
}

$InstallCmd = @"
@echo off
setlocal
set "DIR=%~dp0"
set "CER=%DIR%CustomTwitchChat.cer"
set "MSIX=%DIR%$MsixName"

if exist "%CER%" (
    net session >nul 2>&1
    if %errorlevel% neq 0 (
        echo Requesting administrator permission to trust the signing certificate...
        powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
        exit /b
    )
    echo Installing signing certificate into LocalMachine\TrustedPeople...
    certutil -addstore -f TrustedPeople "%CER%"
    if %errorlevel% neq 0 (
        echo Failed to install certificate.
        pause
        exit /b 1
    )
)

echo Launching Custom Twitch Chat installer...
start "" "%MSIX%"
endlocal
"@
Set-Content -Path (Join-Path $ReleaseDir "Install.cmd") -Value $InstallCmd -Encoding ASCII

$ReadmeInstall = @"
Custom Twitch Chat — Xbox Game Bar Widget (v$SemVer)
====================================================

End-user installation (NO Windows Developer Mode required):

1. Extract all files from this ZIP archive into a folder.
2. Double-click "Install.cmd".
   - If Windows prompts for Administrator permission, click Yes so Windows trusts
     the package signing certificate (one-time step).
   - The native Windows App Installer window will open: click "Install".
3. Open "Custom Twitch Chat" from the Start Menu to configure your channel,
   import a config from the web configurator, or sign in with Twitch.
4. Press Win+G in any game, open Widget Menu -> "Custom Twitch Chat Overlay",
   click the Pin icon and the Click-through icon to keep chat transparent above your game.
"@
Set-Content -Path (Join-Path $ReleaseDir "README-INSTALL.txt") -Value $ReadmeInstall -Encoding UTF8

$ZipPath = Join-Path $RepoRoot "AppPackages\CustomTwitchChat.GameBar-v$SemVer-$Platform.zip"
if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }
Compress-Archive -Path (Join-Path $ReleaseDir "*") -DestinationPath $ZipPath -Force

Write-Host "Release bundle ready: $ZipPath (SelfSigned=$UsesSelfSignedCert)"
