param(
    [string]$PackageDir = "AppPackages"
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$SearchRoot = Join-Path $RepoRoot $PackageDir

$msix = Get-ChildItem -Path $SearchRoot -Recurse -Include *.msix,*.msixbundle,*.appx |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $msix) {
    throw "No .msix package found under '$SearchRoot'. Run .\scripts\package-msix.ps1 first."
}

Write-Host "Installing Xbox Game Bar widget package: $($msix.FullName)"
Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown
Write-Host "Installed. Press Win+G to open Xbox Game Bar and pin 'Custom Twitch Chat Overlay'."
