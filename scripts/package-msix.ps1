param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("x64", "arm64")]
    [string]$Platform = "x64",
    [string]$OutputDir = "AppPackages"
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$AppProject = Join-Path $RepoRoot "src\CustomTwitchChat.GameBar.App\CustomTwitchChat.GameBar.App.csproj"
$WebAssetsDir = Join-Path $RepoRoot "src\CustomTwitchChat.GameBar.App\WebAssets"
$OutPath = Join-Path $RepoRoot $OutputDir

if (-not (Test-Path (Join-Path $WebAssetsDir "overlay.html"))) {
    Write-Host "WebAssets missing; syncing web bundle..."
    if (Get-Command bash -ErrorAction SilentlyContinue) {
        bash (Join-Path $RepoRoot "scripts/sync-web-assets")
    } else {
        throw "Run scripts/sync-web-assets first to populate src/CustomTwitchChat.GameBar.App/WebAssets."
    }
}

New-Item -ItemType Directory -Force -Path $OutPath | Out-Null

dotnet restore $AppProject -p:Platform=$Platform
dotnet build $AppProject `
    -c $Configuration `
    -p:Platform=$Platform `
    -p:AppxPackageDir="$OutPath\" `
    -p:UapAppxPackageBuildMode=SideloadOnly `
    -p:GenerateAppxPackageOnBuild=true

Write-Host "MSIX package output written under: $OutPath"
