using System.Text.Json;
using Microsoft.Gaming.XboxGameBar;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CustomTwitchChat.GameBar.App;

/// <summary>
/// Settings Xbox Game Bar widget view controller with a native XAML keyboard input bridge.
/// </summary>
public sealed partial class SettingsWidgetPage
{
    private readonly App _hostApp;
    private readonly WebView _webView;
    private readonly TextBox _channelBox;
    private readonly TextBlock _statusText;
    private readonly Grid _keypadPanel;
    private Dictionary<string, object> _workingConfig;
    private bool _hydrating;

    public SettingsWidgetPage(App hostApp)
    {
        _hostApp = hostApp;
        _workingConfig = new Dictionary<string, object>(_hostApp.ConfigStore.GetSnapshot().Config, StringComparer.Ordinal);
        _webView = new WebView
        {
            DefaultBackgroundColor = Colors.Transparent,
            AllowFocusOnInteraction = false,
            IsHitTestVisible = false,
        };
        _webView.NavigationCompleted += SettingsWebView_NavigationCompleted;
        _webView.ScriptNotify += SettingsWebView_ScriptNotify;

        _channelBox = CreateTextBox("e.g. peneku_sliby");
        _channelBox.TextChanged += (_, _) => OnFieldEdited();
        _channelBox.KeyDown += OnNativeInputKeyDown;
        _fontNameBox = CreateBoundTextBox("Montserrat");
        _fontSizeBox = CreateBoundTextBox("24");
        _limitBox = CreateBoundTextBox("4");
        _hideAfterBox = CreateBoundTextBox("999");
        _importBox = CreateTextBox("Paste #c=1... URL/hash or StreamElements JSON");

        _presetCombo = CreateCombo(["default", "peneku", "minimal"], OnPresetChanged);
        _langCombo = CreateCombo(["pt-BR", "en"]);
        _layoutCombo = CreateCombo(["block", "inline"]);
        _avatarCombo = CreateCombo(["inline", "block", "none"]);
        _nickColorCombo = CreateCombo(["user", "messagecolor", "custom", "remove"]);
        _animInCombo = CreateCombo(["none", "fadeIn", "fadeInUp", "bounceInLeft", "slideInRight", "zoomIn"]);

        _emote7tvCheck = CreateCheck("7TV");
        _emoteBttvCheck = CreateCheck("BetterTTV");
        _emoteFfzCheck = CreateCheck("FrankerFaceZ");
        _subsCheck = CreateCheck("Subs / Gifts");
        _cheersCheck = CreateCheck("Bits Cheers");
        _raidsCheck = CreateCheck("Raids");
        _streaksCheck = CreateCheck("Watch Streaks");
        _statusText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromArgb(255, 167, 139, 250)),
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        _keypadPanel = BuildMouseKeypad();

        HydrateControlsFromConfig();
        View = new Page
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 15, 15, 20)),
            Content = BuildLayout(),
        };
    }

    public Page View { get; }

    public void AttachWidget(XboxGameBarWidget? widget)
    {
        _ = widget;
        App.Trace("SettingsWidgetPage navigating to overlay.html?preview=1");
        _webView.Navigate(new Uri("ms-appx-web:///WebAssets/overlay.html?preview=1"));
        _channelBox.Focus(FocusState.Programmatic);
    }

    private void OnNativeInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ApplyToOverlay();
            e.Handled = true;
        }
    }

    private void SettingsWebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
    {
        App.Trace($"SettingsWebView NavigationCompleted IsSuccess={args.IsSuccess} WebErrorStatus={args.WebErrorStatus}");
        if (!args.IsSuccess) return;
        PushConfigToPreview();
        TriggerSimulation("testMessage");
    }

    private void SettingsWebView_ScriptNotify(object sender, NotifyEventArgs e)
    {
        App.Trace($"SettingsWebView ScriptNotify: {e.Value}");
        var result = _hostApp.Bridge.HandleIncomingMessage(e.Value);
        if (result.Handled && result.ResponseJson is not null)
        {
            _ = DispatchScriptAsync($"window.__ctcGameBarReceive && window.__ctcGameBarReceive({result.ResponseJson});");
        }
    }

    private Task DispatchScriptAsync(string script) =>
        View.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
        {
            await _webView.InvokeScriptAsync("eval", [script]);
        }).AsTask();
}
