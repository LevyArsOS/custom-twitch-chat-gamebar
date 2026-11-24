using Microsoft.Gaming.XboxGameBar;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace CustomTwitchChat.GameBar.App;

/// <summary>
/// Settings Xbox Game Bar widget view controller hosting the custom-twitch-chat Configurator.
/// </summary>
public sealed class SettingsWidgetPage
{
    private readonly App _hostApp;
    private readonly WebView _webView;

    public SettingsWidgetPage(App hostApp)
    {
        _hostApp = hostApp;
        _webView = new WebView();
        _webView.NavigationCompleted += SettingsWebView_NavigationCompleted;
        _webView.ScriptNotify += SettingsWebView_ScriptNotify;

        var root = new Grid();
        root.Children.Add(_webView);
        View = new Page
        {
            Content = root,
        };
    }

    public Page View { get; }

    public void AttachWidget(XboxGameBarWidget? widget)
    {
        _ = widget;
        App.Trace("SettingsWidgetPage navigating to index.html");
        _webView.Navigate(new Uri("ms-appx-web:///WebAssets/index.html"));
    }

    private void SettingsWebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
    {
        App.Trace($"SettingsWebView NavigationCompleted IsSuccess={args.IsSuccess} WebErrorStatus={args.WebErrorStatus}");
        if (!args.IsSuccess) return;
        var envelope = _hostApp.Bridge.CreateConfigEnvelope(_hostApp.ConfigStore.GetSnapshot());
        _ = DispatchEnvelopeAsync(envelope);
    }

    private void SettingsWebView_ScriptNotify(object sender, NotifyEventArgs e)
    {
        App.Trace($"SettingsWebView ScriptNotify: {e.Value}");
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
