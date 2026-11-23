using CustomTwitchChat.GameBar.Core.Bridge;
using CustomTwitchChat.GameBar.Core.State;
using Microsoft.Gaming.XboxGameBar;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CustomTwitchChat.GameBar.App;

/// <summary>
/// Primary Xbox Game Bar widget view rendering the transparent Twitch chat overlay.
/// </summary>
public sealed class OverlayWidgetPage : Page
{
    private readonly WebView _webView;
    private XboxGameBarWidget? _widget;
    private App HostApp => (App)Application.Current;

    public OverlayWidgetPage()
    {
        Background = new SolidColorBrush(Colors.Transparent);
        _webView = new WebView
        {
            DefaultBackgroundColor = Colors.Transparent,
        };
        _webView.NavigationCompleted += OverlayWebView_NavigationCompleted;
        _webView.ScriptNotify += OverlayWebView_ScriptNotify;

        var root = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
        root.Children.Add(_webView);
        Content = root;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _widget = e.Parameter as XboxGameBarWidget;
        if (_widget is not null)
        {
            _widget.PinnedChanged += OnWidgetStateChanged;
            _widget.ClickThroughEnabledChanged += OnWidgetStateChanged;
            _widget.GameBarDisplayModeChanged += OnWidgetStateChanged;
            _widget.RequestedOpacityChanged += OnWidgetStateChanged;
            _widget.SettingsClicked += OnSettingsClicked;
            HostApp.StateController.OpenSettingsRequested += OnOpenSettingsRequested;
            HostApp.ConfigStore.ConfigChanged += OnConfigChanged;
            PushHostSnapshot();
        }

        _webView.Navigate(new Uri(HostApp.ConfigStore.GetSnapshot().OverlayUri));
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (_widget is not null)
        {
            _widget.PinnedChanged -= OnWidgetStateChanged;
            _widget.ClickThroughEnabledChanged -= OnWidgetStateChanged;
            _widget.GameBarDisplayModeChanged -= OnWidgetStateChanged;
            _widget.RequestedOpacityChanged -= OnWidgetStateChanged;
            _widget.SettingsClicked -= OnSettingsClicked;
        }
        HostApp.StateController.OpenSettingsRequested -= OnOpenSettingsRequested;
        HostApp.ConfigStore.ConfigChanged -= OnConfigChanged;
    }

    private void OnWidgetStateChanged(XboxGameBarWidget sender, object args) => PushHostSnapshot();

    private void PushHostSnapshot()
    {
        if (_widget is null) return;
        var isGameBarOpen = _widget.GameBarDisplayMode != XboxGameBarDisplayMode.PinnedOnly;
        var state = HostApp.StateController.UpdateHostState(new WidgetHostSnapshot(
            IsPinned: _widget.Pinned,
            IsClickThroughEnabled: _widget.ClickThroughEnabled,
            IsGameBarOpen: isGameBarOpen,
            RequestedOpacity: _widget.RequestedOpacity,
            IsFavorited: _widget.Favorited));

        var envelope = HostApp.Bridge.CreateStateEnvelope(state, HostApp.ConfigStore.GetSnapshot());
        _ = DispatchEnvelopeAsync(envelope);
    }

    private void OnConfigChanged(ConfigSnapshot snapshot) =>
        _ = DispatchEnvelopeAsync(HostApp.Bridge.CreateConfigEnvelope(snapshot));

    private async void OnSettingsClicked(XboxGameBarWidget sender, object args) =>
        await sender.ActivateSettingsAsync();

    private void OnOpenSettingsRequested()
    {
        if (_widget is not null)
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => await _widget.ActivateSettingsAsync());
    }

    private void OverlayWebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess) return;
        var envelope = HostApp.Bridge.CreateStateEnvelope(
            HostApp.StateController.CurrentState,
            HostApp.ConfigStore.GetSnapshot());
        _ = DispatchEnvelopeAsync(envelope);
    }

    private void OverlayWebView_ScriptNotify(object sender, NotifyEventArgs e)
    {
        var result = HostApp.Bridge.HandleIncomingMessage(e.Value);
        if (result.Handled && result.ResponseJson is not null)
        {
            _ = DispatchEnvelopeAsync(result.ResponseJson);
        }
    }

    private Task DispatchEnvelopeAsync(string envelopeJson) =>
        Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
        {
            await _webView.InvokeScriptAsync(
                "eval",
                [$"window.__ctcGameBarReceive && window.__ctcGameBarReceive({envelopeJson});"]);
        }).AsTask();
}
