using CustomTwitchChat.GameBar.Core.Bridge;
using CustomTwitchChat.GameBar.Core.Packaging;
using CustomTwitchChat.GameBar.Core.State;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WinRT;

namespace CustomTwitchChat.GameBar.App;

/// <summary>
/// Program entry point bootstrapping the UWP Xbox Game Bar application.
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(static _ => new App());
    }
}

/// <summary>
/// Application entry point routing Xbox Game Bar activations to Overlay and Settings views.
/// </summary>
public sealed partial class App : Application
{
    private XboxGameBarWidget? _overlayWidget;
    private XboxGameBarWidget? _settingsWidget;
    private OverlayWidgetPage? _overlayController;
    private SettingsWidgetPage? _settingsController;

    public App()
    {
        UnhandledException += OnUnhandledException;
        IConfigStorage storage;
        try
        {
            var localDir = ApplicationData.Current.LocalFolder.Path;
            Trace($"App.ctor LocalFolder={localDir}");
            storage = new FileConfigStorage(Path.Combine(localDir, "config.json"));
        }
        catch (Exception ex)
        {
            Trace($"App.ctor fallback storage: {ex.Message}");
            storage = new InMemoryConfigStorage();
        }

        ConfigStore = new ConfigSyncStore(storage);
        StateController = new WidgetStateController();
        Bridge = new BridgeProtocol(ConfigStore, StateController);
    }

    public ConfigSyncStore ConfigStore { get; }
    public WidgetStateController StateController { get; }
    public BridgeProtocol Bridge { get; }

    public static void Trace(string message)
    {
        try
        {
            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "gamebar-trace.log");
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging errors inside restricted sandbox contexts.
        }
    }

    protected override void OnActivated(IActivatedEventArgs args)
    {
        try
        {
            Trace($"OnActivated Kind={args.Kind}");
            var widgetArgs = TryCastWidgetArgs(args);
            if (widgetArgs is null)
            {
                Trace("OnActivated args did not implement IXboxGameBarWidgetActivatedEventArgs");
                return;
            }

            Trace($"OnActivated ExtensionId={widgetArgs.AppExtensionId} IsLaunch={widgetArgs.IsLaunchActivation}");
            if (!widgetArgs.IsLaunchActivation)
            {
                Window.Current.Activate();
                return;
            }

            var rootFrame = new Frame();
            Window.Current.Content = rootFrame;

            if (string.Equals(widgetArgs.AppExtensionId, AppManifestValidator.SettingsWidgetId, StringComparison.Ordinal))
            {
                _settingsWidget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
                _settingsController = new SettingsWidgetPage(this);
                rootFrame.Content = _settingsController.View;
                _settingsController.AttachWidget(_settingsWidget);
            }
            else
            {
                _overlayWidget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
                _overlayController = new OverlayWidgetPage(this);
                rootFrame.Content = _overlayController.View;
                _overlayController.AttachWidget(_overlayWidget);
            }

            Window.Current.Activate();
            Trace("OnActivated completed Window.Current.Activate()");
        }
        catch (Exception ex)
        {
            Trace($"OnActivated ERROR: {ex}");
            throw;
        }
    }

    private static XboxGameBarWidgetActivatedEventArgs? TryCastWidgetArgs(IActivatedEventArgs args)
    {
        if (args is XboxGameBarWidgetActivatedEventArgs direct) return direct;
        try
        {
            return args.As<XboxGameBarWidgetActivatedEventArgs>();
        }
        catch
        {
            return null;
        }
    }

    private static void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Trace($"UnhandledException: {e.Message} | {e.Exception}");
    }
}
