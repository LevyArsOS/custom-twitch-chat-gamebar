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
public sealed class SettingsWidgetPage
{
    private readonly App _hostApp;
    private readonly WebView _webView;
    private readonly TextBlock _fieldLabel;
    private readonly TextBox _inputBox;
    private string _activeFieldKey = "channel";
    private bool _suppressTextChanged;

    public SettingsWidgetPage(App hostApp)
    {
        _hostApp = hostApp;
        _webView = new WebView();
        _webView.NavigationCompleted += SettingsWebView_NavigationCompleted;
        _webView.ScriptNotify += SettingsWebView_ScriptNotify;

        _fieldLabel = new TextBlock
        {
            Text = "Channel:",
            Foreground = new SolidColorBrush(Colors.White),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };

        var initialChannel = _hostApp.ConfigStore.GetSnapshot().Config.TryGetValue("channel", out var ch)
            ? Convert.ToString(ch) ?? string.Empty
            : string.Empty;

        _inputBox = new TextBox
        {
            Text = initialChannel,
            PlaceholderText = "Type Twitch channel (or click any field below to edit with keyboard)...",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        _inputBox.TextChanged += OnNativeInputTextChanged;
        _inputBox.KeyDown += OnNativeInputKeyDown;

        var applyBtn = new Button
        {
            Content = "Apply to Overlay",
            Background = new SolidColorBrush(Color.FromArgb(255, 145, 71, 255)),
            Foreground = new SolidColorBrush(Colors.White),
            VerticalAlignment = VerticalAlignment.Center,
        };
        applyBtn.Click += OnApplyButtonClick;

        var bar = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 20, 20, 28)),
            Padding = new Thickness(12, 8, 12, 8),
        };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_fieldLabel, 0);
        Grid.SetColumn(_inputBox, 1);
        Grid.SetColumn(applyBtn, 2);
        bar.Children.Add(_fieldLabel);
        bar.Children.Add(_inputBox);
        bar.Children.Add(applyBtn);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(bar, 0);
        Grid.SetRow(_webView, 1);
        root.Children.Add(bar);
        root.Children.Add(_webView);

        View = new Page { Content = root };
    }

    public Page View { get; }

    public void AttachWidget(XboxGameBarWidget? widget)
    {
        _ = widget;
        App.Trace("SettingsWidgetPage navigating to index.html");
        _webView.Navigate(new Uri("ms-appx-web:///WebAssets/index.html"));
    }

    private void OnNativeInputTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged) return;
        var textJson = JsonSerializer.Serialize(_inputBox.Text);
        _ = DispatchScriptAsync($"window.__ctcSetActiveInputValue && window.__ctcSetActiveInputValue({textJson});");
    }

    private void OnNativeInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ApplyCurrentInput();
            e.Handled = true;
        }
    }

    private void OnApplyButtonClick(object sender, RoutedEventArgs e) => ApplyCurrentInput();

    private void ApplyCurrentInput()
    {
        var raw = (_inputBox.Text ?? string.Empty).Trim();
        if (_activeFieldKey == "ctc-import-text" ||
            raw.StartsWith("#c=1.", StringComparison.Ordinal) ||
            raw.StartsWith("c=1.", StringComparison.Ordinal) ||
            raw.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                _hostApp.ConfigStore.ApplyInput(raw);
                return;
            }
            catch (Exception ex)
            {
                App.Trace($"ApplyInput failed: {ex.Message}");
            }
        }

        if (_activeFieldKey == "channel" && !string.IsNullOrWhiteSpace(raw))
        {
            var next = new Dictionary<string, object?>(
                _hostApp.ConfigStore.GetSnapshot().Config.ToDictionary(k => k.Key, object? (v) => v.Value),
                StringComparer.Ordinal)
            {
                ["channel"] = raw,
            };
            _hostApp.ConfigStore.ApplyConfig(next);
        }

        _ = DispatchScriptAsync("window.__ctcGameBarApplyNow && window.__ctcGameBarApplyNow();");
    }

    private void SettingsWebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
    {
        App.Trace($"SettingsWebView NavigationCompleted IsSuccess={args.IsSuccess} WebErrorStatus={args.WebErrorStatus}");
        if (!args.IsSuccess) return;
        _inputBox.Focus(FocusState.Programmatic);
        var envelope = _hostApp.Bridge.CreateConfigEnvelope(_hostApp.ConfigStore.GetSnapshot());
        _ = DispatchScriptAsync($"window.__ctcGameBarReceive && window.__ctcGameBarReceive({envelope});");
    }

    private void SettingsWebView_ScriptNotify(object sender, NotifyEventArgs e)
    {
        App.Trace($"SettingsWebView ScriptNotify: {e.Value}");
        if (TryHandleInputFocusMessage(e.Value)) return;

        var result = _hostApp.Bridge.HandleIncomingMessage(e.Value);
        if (result.Handled && result.ResponseJson is not null)
        {
            _ = DispatchScriptAsync($"window.__ctcGameBarReceive && window.__ctcGameBarReceive({result.ResponseJson});");
        }
    }

    private bool TryHandleInputFocusMessage(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("type", out var typeProp) ||
                typeProp.GetString() != "input:focus")
            {
                return false;
            }

            _activeFieldKey = doc.RootElement.TryGetProperty("key", out var k) ? k.GetString() ?? "channel" : "channel";
            var label = doc.RootElement.TryGetProperty("label", out var l) ? l.GetString() ?? _activeFieldKey : _activeFieldKey;
            var value = doc.RootElement.TryGetProperty("value", out var v) ? v.GetString() ?? string.Empty : string.Empty;

            _fieldLabel.Text = $"{label}:";
            _suppressTextChanged = true;
            _inputBox.Text = value;
            _inputBox.SelectionStart = _inputBox.Text.Length;
            _suppressTextChanged = false;
            _inputBox.Focus(FocusState.Programmatic);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private Task DispatchScriptAsync(string script) =>
        View.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
        {
            await _webView.InvokeScriptAsync("eval", [script]);
        }).AsTask();
}
