param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("x64", "arm64")]
    [string]$Platform = "x64",
    [string]$OutputDir = "AppPackages"
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$AppDir = Join-Path $RepoRoot "src\CustomTwitchChat.GameBar.App"
$AppProject = Join-Path $AppDir "CustomTwitchChat.GameBar.App.csproj"
$WebAssetsDir = Join-Path $AppDir "WebAssets"
$OutPath = Join-Path $RepoRoot $OutputDir
$LayoutDir = Join-Path $OutPath "AppxLayout"

if (-not (Test-Path (Join-Path $WebAssetsDir "overlay.html"))) {
    throw "Run scripts/sync-web-assets first to populate src/CustomTwitchChat.GameBar.App/WebAssets."
}

if (Test-Path $LayoutDir) { Remove-Item $LayoutDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $LayoutDir | Out-Null

dotnet publish $AppProject `
    -c $Configuration `
    -p:Platform=$Platform `
    -r "win-$Platform" `
    --self-contained true `
    -o $LayoutDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$ManifestXml = (Get-Content (Join-Path $AppDir "Package.appxmanifest") -Raw)
$ManifestXml = $ManifestXml.Replace('$targetnametoken$', 'CustomTwitchChat.GameBar.App')
$ManifestXml = $ManifestXml -replace '(<Identity\b[^>]*?)(/?>)', "`$1`n    ProcessorArchitecture=`"$Platform`"`$2"
Set-Content -Path (Join-Path $LayoutDir "AppxManifest.xml") -Value $ManifestXml -Encoding UTF8

$AssetsDir = Join-Path $LayoutDir "Assets"
New-Item -ItemType Directory -Force -Path $AssetsDir | Out-Null
Add-Type -AssemblyName System.Drawing
$logos = @{
    "StoreLogo.png" = @(50, 50)
    "Square44x44Logo.png" = @(44, 44)
    "Square150x150Logo.png" = @(150, 150)
    "Wide310x150Logo.png" = @(310, 150)
}
foreach ($entry in $logos.GetEnumerator()) {
    $bmp = New-Object System.Drawing.Bitmap($entry.Value[0], $entry.Value[1])
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(255, 145, 71, 255))
    $g.Dispose()
    $bmp.Save((Join-Path $AssetsDir $entry.Key), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

$MakeAppx = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter "makeappx.exe" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "\\$Platform\\makeappx\.exe$" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if ($MakeAppx) {
    $MsixFile = Join-Path $OutPath "CustomTwitchChat.GameBar_$Platform.msix"
    & $MakeAppx.FullName pack /o /d $LayoutDir /p $MsixFile
    if ($LASTEXITCODE -ne 0) { throw "makeappx.exe failed with exit code $LASTEXITCODE" }
    Write-Host "MSIX package written to: $MsixFile"
} else {
    Write-Host "Appx layout staged for sideload registration at: $LayoutDir\AppxManifest.xml"
}
