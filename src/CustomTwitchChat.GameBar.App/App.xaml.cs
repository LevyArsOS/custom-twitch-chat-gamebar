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
    private SettingsWidgetPage? _desktopController;

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
        ConfigStore.ConfigChanged += snap =>
        {
            try
            {
                _lastAppliedJson = System.Text.Json.JsonSerializer.Serialize(snap.Config);
            }
            catch
            {
                // Ignore serialization errors.
            }
        };
        StateController = new WidgetStateController();
        Bridge = new BridgeProtocol(ConfigStore, StateController);
        StartExternalConfigWatcher();
    }

    private FileSystemWatcher? _configWatcher;
    private string _lastAppliedJson = string.Empty;

    private void StartExternalConfigWatcher()
    {
        try
        {
            var dir = ApplicationData.Current.LocalFolder.Path;
            _configWatcher = new FileSystemWatcher(dir, "config.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _configWatcher.Changed += OnExternalConfigFileChanged;
            _configWatcher.Created += OnExternalConfigFileChanged;
        }
        catch
        {
            // Ignore watcher errors in restricted environments.
        }
    }

    private void OnExternalConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            Thread.Sleep(50);
            if (!File.Exists(e.FullPath)) return;
            var raw = File.ReadAllText(e.FullPath);
            if (string.IsNullOrWhiteSpace(raw) || raw == _lastAppliedJson) return;
            _lastAppliedJson = raw;
            ConfigStore.ApplyInput(raw);
            Trace("Reloaded config.json from external update.");
        }
        catch
        {
            // Ignore transient file lock while writer finishes.
        }
    }

    public ConfigSyncStore ConfigStore { get; }
    public WidgetStateController StateController { get; }
    public BridgeProtocol Bridge { get; }
    public event Action? AuthSessionUpdated;

    public void SaveAuthSession(string key, string sessionJson)
    {
        try
        {
            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "auth-session.json");
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { key, sessionJson }));
        }
        catch
        {
            // Ignore sandbox write errors.
        }
        AuthSessionUpdated?.Invoke();
    }

    public (string Key, string SessionJson)? LoadAuthSession()
    {
        try
        {
            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "auth-session.json");
            if (!File.Exists(path)) return null;
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            var k = doc.RootElement.GetProperty("key").GetString();
            var s = doc.RootElement.GetProperty("sessionJson").GetString();
            return !string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(s) ? (k, s) : null;
        }
        catch
        {
            return null;
        }
    }

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

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Trace($"OnLaunched Arguments={args.Arguments}");
        OpenDesktopConfigurator(null);
    }

    protected override void OnActivated(IActivatedEventArgs args)
    {
        try
        {
            Trace($"OnActivated Kind={args.Kind}");
            if (args is IProtocolActivatedEventArgs proto &&
                string.Equals(proto.Uri?.Scheme, "ctc-gamebar", StringComparison.OrdinalIgnoreCase))
            {
                HandleCustomProtocol(proto.Uri);
                return;
            }

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
                _settingsController = new SettingsWidgetPage(this, isDesktopMode: false);
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

    private void HandleCustomProtocol(Uri uri)
    {
        Trace($"HandleCustomProtocol Uri={uri.AbsoluteUri}");
        string? status = null;
        var raw = uri.AbsoluteUri;
        var idx = raw.IndexOf("c=1.", StringComparison.Ordinal);
        if (idx >= 0)
        {
            var payload = "#" + raw[idx..];
            var snap = ConfigStore.ApplyInput(payload);
            status = $"Imported from link (#{snap.Config["channel"]}) — applied to Game Bar Overlay!";
        }
        OpenDesktopConfigurator(status);
    }

    private void OpenDesktopConfigurator(string? initialStatus)
    {
        if (Window.Current.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            Window.Current.Content = rootFrame;
        }

        if (_desktopController is null || rootFrame.Content != _desktopController.View)
        {
            _desktopController = new SettingsWidgetPage(this, isDesktopMode: true);
            rootFrame.Content = _desktopController.View;
            _desktopController.AttachWidget(null);
        }

        if (!string.IsNullOrEmpty(initialStatus))
        {
            _desktopController.SetStatus(initialStatus);
        }

        try
        {
            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().Title = "Custom Twitch Chat — Configurator";
        }
        catch
        {
            // Ignore if view title is unavailable.
        }

        Window.Current.Activate();
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
