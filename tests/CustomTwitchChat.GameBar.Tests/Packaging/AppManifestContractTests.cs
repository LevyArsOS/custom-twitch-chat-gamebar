using CustomTwitchChat.GameBar.Core.Packaging;
using Xunit;

namespace CustomTwitchChat.GameBar.Tests.Packaging;

public sealed class AppManifestContractTests
{
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CustomTwitchChat.GameBar.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new DirectoryNotFoundException("Solution root not found.");
    }

    [Fact]
    public void PackageManifest_SatisfiesXboxGameBarSdkContract()
    {
        var manifestPath = Path.Combine(FindRepoRoot(), "src", "CustomTwitchChat.GameBar.App", "Package.appxmanifest");
        var result = AppManifestValidator.ValidateFile(manifestPath);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Empty(result.Errors);
        Assert.Contains(AppManifestValidator.OverlayWidgetId, result.WidgetIds);
        Assert.Contains(AppManifestValidator.SettingsWidgetId, result.WidgetIds);
    }

    [Fact]
    public void ValidateFile_And_ValidateXml_RejectInvalidAndIncompleteManifests()
    {
        Assert.False(AppManifestValidator.ValidateFile(null).IsValid);
        Assert.False(AppManifestValidator.ValidateFile(Path.Combine(FindRepoRoot(), "does-not-exist.xml")).IsValid);
        Assert.False(AppManifestValidator.ValidateXml(" ").IsValid);
        Assert.False(AppManifestValidator.ValidateXml("<broken").IsValid);
        Assert.False(AppManifestValidator.ValidateXml("<NotPackage />").IsValid);

        const string missingAll = """
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                 xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3">
          <Applications><Application Id="App"><Extensions /></Application></Applications>
        </Package>
        """;
        var missingResult = AppManifestValidator.ValidateXml(missingAll);
        Assert.False(missingResult.IsValid);
        Assert.Contains(missingResult.Errors, e => e.Contains("internetClient", StringComparison.Ordinal));
        Assert.Contains(missingResult.Errors, e => e.Contains(AppManifestValidator.OverlayWidgetId, StringComparison.Ordinal));
        Assert.Contains(missingResult.Errors, e => e.Contains(AppManifestValidator.SettingsWidgetId, StringComparison.Ordinal));

        const string wrongTypes = """
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                 xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3">
          <Capabilities><Capability Name="internetClient" /></Capabilities>
          <Applications><Application Id="App"><Extensions>
            <uap3:Extension Category="windows.appExtension">
              <uap3:AppExtension Name="microsoft.gameBarUIExtension" Id="CustomTwitchChat.Overlay">
                <uap3:Properties><GameBarWidget Type="Settings" /></uap3:Properties>
              </uap3:AppExtension>
            </uap3:Extension>
            <uap3:Extension Category="windows.appExtension">
              <uap3:AppExtension Name="microsoft.gameBarUIExtension" Id="CustomTwitchChat.Settings">
                <uap3:Properties><GameBarWidget Type="Standard" /></uap3:Properties>
              </uap3:AppExtension>
            </uap3:Extension>
          </Extensions></Application></Applications>
        </Package>
        """;
        var wrongTypesResult = AppManifestValidator.ValidateXml(wrongTypes);
        Assert.False(wrongTypesResult.IsValid);
        Assert.Equal(2, wrongTypesResult.Errors.Count);

        const string missingFlags = """
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                 xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3">
          <Capabilities><Capability Name="internetClient" /></Capabilities>
          <Applications><Application Id="App"><Extensions>
            <uap3:Extension Category="windows.appExtension">
              <uap3:AppExtension Name="microsoft.gameBarUIExtension" Id="CustomTwitchChat.Overlay">
                <uap3:Properties>
                  <GameBarWidget Type="Standard">
                    <AllowForegroundTransparency>false</AllowForegroundTransparency>
                    <SettingsSupported AppExtensionId="Wrong.Id" />
                  </GameBarWidget>
                </uap3:Properties>
              </uap3:AppExtension>
            </uap3:Extension>
            <uap3:Extension Category="windows.appExtension">
              <uap3:AppExtension Name="microsoft.gameBarUIExtension" Id="CustomTwitchChat.Settings">
                <uap3:Properties><GameBarWidget Type="Settings" /></uap3:Properties>
              </uap3:AppExtension>
            </uap3:Extension>
          </Extensions></Application></Applications>
        </Package>
        """;
        var flagsResult = AppManifestValidator.ValidateXml(missingFlags);
        Assert.False(flagsResult.IsValid);
        Assert.Equal(4, flagsResult.Errors.Count);
    }
}
