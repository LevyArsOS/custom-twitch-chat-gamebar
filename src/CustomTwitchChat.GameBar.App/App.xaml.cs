using CustomTwitchChat.GameBar.Core.Bridge;
using CustomTwitchChat.GameBar.Core.Packaging;
using CustomTwitchChat.GameBar.Core.State;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CustomTwitchChat.GameBar.App;

/// <summary>
/// Program entry point bootstrapping the UWP Xbox Game Bar application.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ => _ = new App());
    }
}

/// <summary>
/// Application entry point routing Xbox Game Bar activations to Overlay and Settings views.
/// </summary>
public sealed class App : Application
{
    private XboxGameBarWidget? _overlayWidget;
    private XboxGameBarWidget? _settingsWidget;

    public App()
    {
        var configPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "config.json");
        ConfigStore = new ConfigSyncStore(new FileConfigStorage(configPath));
        StateController = new WidgetStateController();
        Bridge = new BridgeProtocol(ConfigStore, StateController);
    }

    public ConfigSyncStore ConfigStore { get; }
    public WidgetStateController StateController { get; }
    public BridgeProtocol Bridge { get; }

    protected override void OnActivated(IActivatedEventArgs args)
    {
        if (args is not XboxGameBarWidgetActivatedEventArgs widgetArgs || !widgetArgs.IsLaunchActivation)
        {
            return;
        }

        var rootFrame = new Frame();
        Window.Current.Content = rootFrame;

        if (string.Equals(widgetArgs.AppExtensionId, AppManifestValidator.SettingsWidgetId, StringComparison.Ordinal))
        {
            _settingsWidget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
            rootFrame.Navigate(typeof(SettingsWidgetPage), _settingsWidget);
        }
        else
        {
            _overlayWidget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
            rootFrame.Navigate(typeof(OverlayWidgetPage), _overlayWidget);
        }

        Window.Current.Activate();
    }
}
