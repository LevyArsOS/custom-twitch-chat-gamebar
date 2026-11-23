using CustomTwitchChat.GameBar.Core.State;
using Xunit;

namespace CustomTwitchChat.GameBar.Tests.Packaging;

public sealed class WebBridgeContractTests
{
    [Fact]
    public void GameBarBridgeScript_MatchesCoreStateAndProtocolContracts()
    {
        var repoRoot = AppManifestContractTests.FindRepoRoot();
        var bridgePath = Path.Combine(repoRoot, "src", "WebBridge", "gamebar-bridge.js");
        Assert.True(File.Exists(bridgePath));

        var source = File.ReadAllText(bridgePath);
        Assert.Contains("ctc.generator.v1", source, StringComparison.Ordinal);
        Assert.Contains("ctc.gamebar.hash.v1", source, StringComparison.Ordinal);
        Assert.Contains("ctc.gamebar.v1", source, StringComparison.Ordinal);
        Assert.Contains(WidgetStateController.PinnedClickThroughClass, source, StringComparison.Ordinal);
        Assert.Contains(WidgetStateController.PinnedInteractiveClass, source, StringComparison.Ordinal);
        Assert.Contains(WidgetStateController.UnpinnedClass, source, StringComparison.Ordinal);
        Assert.Contains("state:query", source, StringComparison.Ordinal);
        Assert.Contains("state:update", source, StringComparison.Ordinal);
        Assert.Contains("config:apply", source, StringComparison.Ordinal);
        Assert.Contains("config:updated", source, StringComparison.Ordinal);
        Assert.Contains("__ctcGameBarReceive", source, StringComparison.Ordinal);
        Assert.Contains("Apply to Game Bar Overlay", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncedWebAssets_InjectBridgeScriptIntoOverlayAndSettingsPages()
    {
        var repoRoot = AppManifestContractTests.FindRepoRoot();
        var assetsDir = Path.Combine(repoRoot, "src", "CustomTwitchChat.GameBar.App", "WebAssets");
        var overlayHtmlPath = Path.Combine(assetsDir, "overlay.html");
        var indexHtmlPath = Path.Combine(assetsDir, "index.html");
        var bridgeAssetPath = Path.Combine(assetsDir, "gamebar-bridge.js");

        Assert.True(File.Exists(overlayHtmlPath), "Run scripts/sync-web-assets before running tests.");
        Assert.True(File.Exists(indexHtmlPath), "Run scripts/sync-web-assets before running tests.");
        Assert.True(File.Exists(bridgeAssetPath), "Run scripts/sync-web-assets before running tests.");

        var overlayHtml = File.ReadAllText(overlayHtmlPath);
        var bridgeIndex = overlayHtml.IndexOf("./gamebar-bridge.js", StringComparison.Ordinal);
        var runtimeIndex = overlayHtml.IndexOf("runtime/overlay.js", StringComparison.Ordinal);
        Assert.True(bridgeIndex >= 0, "overlay.html must reference ./gamebar-bridge.js");
        Assert.True(runtimeIndex > bridgeIndex, "./gamebar-bridge.js must load before runtime/overlay.js");

        var indexHtml = File.ReadAllText(indexHtmlPath);
        Assert.Contains("./gamebar-bridge.js", indexHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagingScripts_ExistAndReferenceExpectedManifestAndAppProject()
    {
        var repoRoot = AppManifestContractTests.FindRepoRoot();
        var packageScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "package-msix.ps1"));
        var installScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "install-dev-msix.ps1"));

        Assert.Contains("CustomTwitchChat.GameBar.App.csproj", packageScript, StringComparison.Ordinal);
        Assert.Contains("GenerateAppxPackageOnBuild=true", packageScript, StringComparison.Ordinal);
        Assert.Contains("Add-AppxPackage", installScript, StringComparison.Ordinal);
    }
}
