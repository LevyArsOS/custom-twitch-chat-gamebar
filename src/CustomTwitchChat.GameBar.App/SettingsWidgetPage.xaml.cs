using CustomTwitchChat.GameBar.Core.Bridge;
using Microsoft.Gaming.XboxGameBar;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace CustomTwitchChat.GameBar.App;

/// <summary>
/// Settings Xbox Game Bar widget view hosting the custom-twitch-chat Configurator.
/// </summary>
public sealed partial class SettingsWidgetPage : Page
{
    private App HostApp => (App)Application.Current;

    public SettingsWidgetPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = e.Parameter as XboxGameBarWidget;
        SettingsWebView.Navigate(new Uri("ms-appx-web:///WebAssets/index.html"));
    }

    private void SettingsWebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess) return;
        var envelope = HostApp.Bridge.CreateConfigEnvelope(HostApp.ConfigStore.GetSnapshot());
        _ = DispatchEnvelopeAsync(envelope);
    }

    private void SettingsWebView_ScriptNotify(object sender, NotifyEventArgs e)
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
            await SettingsWebView.InvokeScriptAsync(
                "eval",
                [$"window.__ctcGameBarReceive && window.__ctcGameBarReceive({envelopeJson});"]);
        }).AsTask();
}
