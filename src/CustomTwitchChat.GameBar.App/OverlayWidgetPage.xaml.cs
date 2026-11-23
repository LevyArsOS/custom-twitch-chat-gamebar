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
    private XboxGameBarWidget? _widget;

    public OverlayWidgetPage(App hostApp)
    {
        _hostApp = hostApp;
        _webView = new WebView
        {
            DefaultBackgroundColor = Colors.Transparent,
        };
        _webView.NavigationCompleted += OverlayWebView_NavigationCompleted;
        _webView.ScriptNotify += OverlayWebView_ScriptNotify;

        var root = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
        root.Children.Add(_webView);
        View = new Page
        {
            Background = new SolidColorBrush(Colors.Transparent),
            Content = root,
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
            PushHostSnapshot();
        }

        var uri = _hostApp.ConfigStore.GetSnapshot().OverlayUri;
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
        _ = DispatchEnvelopeAsync(envelope);
    }

    private void OnConfigChanged(ConfigSnapshot snapshot) =>
        _ = DispatchEnvelopeAsync(_hostApp.Bridge.CreateConfigEnvelope(snapshot));

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
        var envelope = _hostApp.Bridge.CreateStateEnvelope(
            _hostApp.StateController.CurrentState,
            _hostApp.ConfigStore.GetSnapshot());
        _ = DispatchEnvelopeAsync(envelope);
    }

    private void OverlayWebView_ScriptNotify(object sender, NotifyEventArgs e)
    {
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
