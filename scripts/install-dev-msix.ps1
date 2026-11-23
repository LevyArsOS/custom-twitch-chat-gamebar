param(
    [string]$PackageDir = "AppPackages"
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$SearchRoot = Join-Path $RepoRoot $PackageDir
$ManifestPath = Join-Path $SearchRoot "AppxLayout\AppxManifest.xml"

$msix = Get-ChildItem -Path $SearchRoot -Recurse -Include *.msix,*.msixbundle,*.appx -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($msix) {
    Write-Host "Installing Xbox Game Bar widget package: $($msix.FullName)"
    Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown
} elseif (Test-Path $ManifestPath) {
    Write-Host "Registering Xbox Game Bar widget layout: $ManifestPath"
    Add-AppxPackage -Register $ManifestPath -ForceApplicationShutdown
} else {
    throw "No .msix or AppxLayout\AppxManifest.xml found under '$SearchRoot'. Run .\scripts\package-msix.ps1 first."
}

Write-Host "Installed. Press Win+G to open Xbox Game Bar and pin 'Custom Twitch Chat Overlay'."
