param(
    [Parameter(Position = 0)]
    [string]$InputValue = "",
    [string]$Channel = "",
    [string]$ClientId = "",
    [switch]$EnableOAuth
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Pkg = Get-AppxPackage -Name "CustomTwitchChat.GameBarWidget" | Select-Object -First 1
if (-not $Pkg) {
    throw "CustomTwitchChat.GameBarWidget package is not installed. Run scripts/install-dev-msix.ps1 first."
}
$LocalStateDir = Join-Path $env:LOCALAPPDATA "Packages\$($Pkg.PackageFamilyName)\LocalState"
$ConfigPath = Join-Path $LocalStateDir "config.json"

New-Item -ItemType Directory -Force -Path $LocalStateDir | Out-Null

$Code = @"
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CustomTwitchChat.GameBar.Core.Config;

public static class ConfigApplier {
    public static void Run(string configPath, string inputVal, string channel, string clientId, bool enableOAuth) {
        Dictionary<string, object> cfg;
        if (!string.IsNullOrWhiteSpace(inputVal)) {
            var raw = File.Exists(inputVal) ? File.ReadAllText(inputVal) : inputVal;
            cfg = ConfigCodec.ImportConfigFromInput(raw);
        } else if (File.Exists(configPath)) {
            cfg = ConfigCodec.NormalizeJson(File.ReadAllText(configPath));
        } else {
            cfg = ConfigCodec.GetDefaultConfig();
        }
        if (!string.IsNullOrWhiteSpace(channel)) cfg["channel"] = channel.Trim().TrimStart('#', '@').ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(clientId)) cfg["clientId"] = clientId.Trim();
        if (enableOAuth) {
            cfg["authEnabled"] = "yes";
            cfg["authPoints"] = "yes";
            cfg["authFollows"] = "yes";
        }
        var norm = ConfigCodec.NormalizeObjectConfig(cfg.ToDictionary(k => k.Key, k => (object)k.Value));
        File.WriteAllText(configPath, JsonSerializer.Serialize(norm));
        Console.WriteLine("Applied config for #" + norm["channel"] + " -> " + ConfigCodec.EncodeConfigHash(norm));
    }
}
"@

$CoreDll = Get-ChildItem -Path (Join-Path $RepoRoot "src\CustomTwitchChat.GameBar.Core\bin") -Recurse -Filter "CustomTwitchChat.GameBar.Core.dll" |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $CoreDll) {
    dotnet build (Join-Path $RepoRoot "src\CustomTwitchChat.GameBar.Core\CustomTwitchChat.GameBar.Core.csproj") -c Release --nologo | Out-Null
    $CoreDll = Get-ChildItem -Path (Join-Path $RepoRoot "src\CustomTwitchChat.GameBar.Core\bin") -Recurse -Filter "CustomTwitchChat.GameBar.Core.dll" |
        Select-Object -First 1 -ExpandProperty FullName
}

Add-Type -TypeDefinition $Code -ReferencedAssemblies $CoreDll, "System.Text.Json", "System.Linq", "System.Collections"
[ConfigApplier]::Run($ConfigPath, $InputValue, $Channel, $ClientId, [bool]$EnableOAuth)
