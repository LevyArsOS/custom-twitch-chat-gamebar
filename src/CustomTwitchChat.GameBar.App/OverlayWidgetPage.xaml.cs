using CustomTwitchChat.GameBar.Core.Bridge;
using CustomTwitchChat.GameBar.Core.State;
using Microsoft.Gaming.XboxGameBar;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CustomTwitchChat.GameBar.App;

/// <summary>
/// Primary Xbox Game Bar widget view controller rendering the transparent Twitch chat overlay.
/// </summary>
public sealed class OverlayWidgetPage
{
    private readonly App _hostApp;
    private readonly WebView _webView;
    private readonly Grid _rootGrid;
    private XboxGameBarWidget? _widget;
    private int _revision;

    public OverlayWidgetPage(App hostApp)
    {
        _hostApp = hostApp;
        _webView = new WebView
        {
            DefaultBackgroundColor = Colors.Transparent,
            AllowFocusOnInteraction = false,
            IsHitTestVisible = false,
        };
        _webView.NavigationCompleted += OverlayWebView_NavigationCompleted;
        _webView.ScriptNotify += OverlayWebView_ScriptNotify;

        _rootGrid = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
        _rootGrid.Children.Add(_webView);
        View = new Page
        {
            Background = new SolidColorBrush(Colors.Transparent),
            Content = _rootGrid,
        };
        View.Unloaded += (_, _) => DetachWidget();
    }

    public Page View { get; }

    public void AttachWidget(XboxGameBarWidget? widget)
    {
        DetachWidget();
        _widget = widget;
        if (_widget is not null)
        {
            _widget.PinnedChanged += OnWidgetStateChanged;
            _widget.ClickThroughEnabledChanged += OnWidgetStateChanged;
            _widget.GameBarDisplayModeChanged += OnWidgetStateChanged;
            _widget.RequestedOpacityChanged += OnWidgetStateChanged;
            _widget.SettingsClicked += OnSettingsClicked;
            _hostApp.StateController.OpenSettingsRequested += OnOpenSettingsRequested;
            _hostApp.ConfigStore.ConfigChanged += OnConfigChanged;
            _hostApp.AuthSessionUpdated += OnAuthSessionUpdated;
            PushHostSnapshot();
        }

        NavigateToSnapshot(_hostApp.ConfigStore.GetSnapshot());
    }

    private void NavigateToSnapshot(ConfigSnapshot snapshot)
    {
        var rev = Interlocked.Increment(ref _revision);
        var uri = $"ms-appx-web:///WebAssets/overlay.html?rev={rev}{snapshot.Hash}";
        App.Trace($"OverlayWidgetPage navigating to {uri}");
        _webView.Navigate(new Uri(uri));
    }

    private void DetachWidget()
    {
        if (_widget is not null)
        {
            _widget.PinnedChanged -= OnWidgetStateChanged;
            _widget.ClickThroughEnabledChanged -= OnWidgetStateChanged;
            _widget.GameBarDisplayModeChanged -= OnWidgetStateChanged;
            _widget.RequestedOpacityChanged -= OnWidgetStateChanged;
            _widget.SettingsClicked -= OnSettingsClicked;
            _widget = null;
        }
        _hostApp.StateController.OpenSettingsRequested -= OnOpenSettingsRequested;
        _hostApp.ConfigStore.ConfigChanged -= OnConfigChanged;
        _hostApp.AuthSessionUpdated -= OnAuthSessionUpdated;
    }

    private void OnAuthSessionUpdated() =>
        _ = View.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, InjectSavedAuthSession);

    private void InjectSavedAuthSession()
    {
        var saved = _hostApp.LoadAuthSession();
        if (saved is null) return;
        var k = System.Text.Json.JsonSerializer.Serialize(saved.Value.Key);
        var s = System.Text.Json.JsonSerializer.Serialize(saved.Value.SessionJson);
        _ = View.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
        {
            await _webView.InvokeScriptAsync("eval", [$"window.__ctcInjectAuthSession && window.__ctcInjectAuthSession({k}, {s});"]);
        });
    }

    private void OnWidgetStateChanged(XboxGameBarWidget sender, object args) => PushHostSnapshot();

    private void PushHostSnapshot()
    {
        if (_widget is null) return;
        var isGameBarOpen = _widget.GameBarDisplayMode != XboxGameBarDisplayMode.PinnedOnly;
        var state = _hostApp.StateController.UpdateHostState(new WidgetHostSnapshot(
            IsPinned: _widget.Pinned,
            IsClickThroughEnabled: _widget.ClickThroughEnabled,
            IsGameBarOpen: isGameBarOpen,
            RequestedOpacity: _widget.RequestedOpacity,
            IsFavorited: _widget.Favorited));

        var envelope = _hostApp.Bridge.CreateStateEnvelope(state, _hostApp.ConfigStore.GetSnapshot());
        _ = View.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        {
            var interactive = !state.IsTransparentClickThrough;
            _webView.IsHitTestVisible = false;
            _rootGrid.IsHitTestVisible = interactive;
            View.IsHitTestVisible = interactive;
            try
            {
                Windows.UI.Xaml.Window.Current.CoreWindow.IsInputEnabled = interactive;
            }
            catch
            {
                // Ignore if CoreWindow input state is managed exclusively by Game Bar host.
            }
        });
        _ = DispatchEnvelopeAsync(envelope);
    }

    private void OnConfigChanged(ConfigSnapshot snapshot)
    {
        _ = View.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => NavigateToSnapshot(snapshot));
    }

    private async void OnSettingsClicked(XboxGameBarWidget sender, object args) =>
        await sender.ActivateSettingsAsync();

    private void OnOpenSettingsRequested()
    {
        if (_widget is not null)
            _ = View.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => await _widget.ActivateSettingsAsync());
    }

    private void OverlayWebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
    {
        App.Trace($"OverlayWebView NavigationCompleted IsSuccess={args.IsSuccess} WebErrorStatus={args.WebErrorStatus}");
        if (!args.IsSuccess) return;
        InjectSavedAuthSession();
        var envelope = _hostApp.Bridge.CreateStateEnvelope(
            _hostApp.StateController.CurrentState,
            _hostApp.ConfigStore.GetSnapshot());
        _ = DispatchEnvelopeAsync(envelope);
    }

    private void OverlayWebView_ScriptNotify(object sender, NotifyEventArgs e)
    {
        App.Trace($"OverlayWebView ScriptNotify: {e.Value}");
        var result = _hostApp.Bridge.HandleIncomingMessage(e.Value);
        if (result.Handled && result.ResponseJson is not null)
        {
            _ = DispatchEnvelopeAsync(result.ResponseJson);
        }
    }

    private Task DispatchEnvelopeAsync(string envelopeJson) =>
        View.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
        {
            await _webView.InvokeScriptAsync(
                "eval",
                [$"window.__ctcGameBarReceive && window.__ctcGameBarReceive({envelopeJson});"]);
        }).AsTask();
}
