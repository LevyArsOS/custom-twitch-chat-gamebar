using System.Globalization;
using CustomTwitchChat.GameBar.Core.Config;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CustomTwitchChat.GameBar.App;

public sealed partial class SettingsWidgetPage
{
    private void HydrateControlsFromConfig()
    {
        _hydrating = true;
        _channelBox.Text = GetStr("channel");
        _fontNameBox.Text = GetStr("fontName");
        _fontSizeBox.Text = GetNumStr("fontSize", 24);
        _limitBox.Text = GetNumStr("messagesLimit", 4);
        _hideAfterBox.Text = GetNumStr("hideAfter", 999);
        SetComboValue(_langCombo, GetStr("lang", "pt-BR"));
        SetComboValue(_layoutCombo, GetStr("textInSameLine", "block"));
        SetComboValue(_avatarCombo, GetStr("showAvatar", "inline"));
        SetComboValue(_nickColorCombo, GetStr("nickColor", "user"));
        SetComboValue(_animInCombo, GetStr("animationIn", "none"));
        _emote7tvCheck.IsChecked = GetStr("enable7tv", "yes") == "yes";
        _emoteBttvCheck.IsChecked = GetStr("enableBttv", "yes") == "yes";
        _emoteFfzCheck.IsChecked = GetStr("enableFfz", "yes") == "yes";
        _subsCheck.IsChecked = GetStr("showSubs", "yes") == "yes";
        _cheersCheck.IsChecked = GetStr("showCheers", "yes") == "yes";
        _raidsCheck.IsChecked = GetStr("showRaids", "yes") == "yes";
        _streaksCheck.IsChecked = GetStr("showStreaks", "yes") == "yes";
        _authEnabledCheck.IsChecked = GetStr("authEnabled", "no") == "yes";
        _authPointsCheck.IsChecked = GetStr("authPoints", "no") == "yes";
        _authFollowsCheck.IsChecked = GetStr("authFollows", "no") == "yes";
        _clientIdBox.Text = GetStr("clientId");
        _hydrating = false;
    }

    private void ReadControlsIntoWorkingConfig()
    {
        _workingConfig["channel"] = (_channelBox.Text ?? string.Empty).Trim().TrimStart('#', '@').ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(_fontNameBox.Text)) _workingConfig["fontName"] = _fontNameBox.Text.Trim();
        if (double.TryParse(_fontSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fs)) _workingConfig["fontSize"] = fs;
        if (double.TryParse(_limitBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lim)) _workingConfig["messagesLimit"] = lim;
        if (double.TryParse(_hideAfterBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var ha)) _workingConfig["hideAfter"] = ha;
        if (_langCombo.SelectedItem is string lang) _workingConfig["lang"] = lang;
        if (_layoutCombo.SelectedItem is string layout) _workingConfig["textInSameLine"] = layout;
        if (_avatarCombo.SelectedItem is string av) _workingConfig["showAvatar"] = av;
        if (_nickColorCombo.SelectedItem is string nc) _workingConfig["nickColor"] = nc;
        if (_animInCombo.SelectedItem is string anim) _workingConfig["animationIn"] = anim;
        _workingConfig["enable7tv"] = _emote7tvCheck.IsChecked == true ? "yes" : "no";
        _workingConfig["enableBttv"] = _emoteBttvCheck.IsChecked == true ? "yes" : "no";
        _workingConfig["enableFfz"] = _emoteFfzCheck.IsChecked == true ? "yes" : "no";
        _workingConfig["showSubs"] = _subsCheck.IsChecked == true ? "yes" : "no";
        _workingConfig["showCheers"] = _cheersCheck.IsChecked == true ? "yes" : "no";
        _workingConfig["showRaids"] = _raidsCheck.IsChecked == true ? "yes" : "no";
        _workingConfig["showStreaks"] = _streaksCheck.IsChecked == true ? "yes" : "no";
        _workingConfig["authEnabled"] = _authEnabledCheck.IsChecked == true ? "yes" : "no";
        _workingConfig["authPoints"] = _authPointsCheck.IsChecked == true ? "yes" : "no";
        _workingConfig["authFollows"] = _authFollowsCheck.IsChecked == true ? "yes" : "no";
        _workingConfig["clientId"] = (_clientIdBox.Text ?? string.Empty).Trim();
    }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_hydrating || _presetCombo.SelectedItem is not string preset) return;
        var currentChannel = (_channelBox.Text ?? string.Empty).Trim();
        var next = ConfigCodec.GetDefaultConfig();
        if (preset == "peneku")
        {
            next["fontName"] = "Permanent Marker";
            next["emoteSoloSize"] = 120d;
            next["messagesLimit"] = 10d;
            next["textBackgroundColor"] = "rgba(154, 140, 153, 0.7)";
            next["animationIn"] = "bounceInLeft";
            next["animationOut"] = "fadeOut";
            next["avatarSize"] = 50d;
        }
        else if (preset == "minimal")
        {
            next["fontName"] = "Inter";
            next["fontSize"] = 20d;
            next["fontWeight"] = "500";
            next["showAvatar"] = "none";
            next["displayBadges"] = "none";
            next["textInSameLine"] = "inline";
            next["textBackgroundColor"] = "rgba(18, 18, 22, 0.72)";
            next["textBorderRadius"] = 8d;
            next["textBorderWidth"] = 0d;
            next["messagesLimit"] = 8d;
            next["hideAfter"] = 30d;
            next["animationIn"] = "fadeInUp";
            next["animationOut"] = "fadeOut";
        }
        next["channel"] = currentChannel;
        _workingConfig = next;
        HydrateControlsFromConfig();
        ApplyToOverlay();
    }

    private static void SetComboValue(ComboBox combo, string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (!combo.Items.Contains(value)) combo.Items.Add(value);
        combo.SelectedItem = value;
    }

    private string GetStr(string key, string fallback = "") =>
        _workingConfig.TryGetValue(key, out var v) && v is not null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? fallback : fallback;

    private string GetNumStr(string key, double fallback) =>
        _workingConfig.TryGetValue(key, out var v) && v is not null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? fallback.ToString(CultureInfo.InvariantCulture) : fallback.ToString(CultureInfo.InvariantCulture);

    private static TextBox CreateTextBox(string placeholder) => new()
    {
        PlaceholderText = placeholder,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private TextBox CreateBoundTextBox(string placeholder)
    {
        var tb = CreateTextBox(placeholder);
        tb.TextChanged += (_, _) => OnFieldEdited();
        return tb;
    }

    private ComboBox CreateCombo(IEnumerable<string> items, SelectionChangedEventHandler? handler = null)
    {
        var cb = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var item in items) cb.Items.Add(item);
        cb.SelectedIndex = 0;
        cb.SelectionChanged += handler ?? ((_, _) => OnFieldEdited());
        return cb;
    }

    private CheckBox CreateCheck(string label, bool defaultChecked = true)
    {
        var chk = new CheckBox { Content = label, Foreground = new SolidColorBrush(Colors.White), IsChecked = defaultChecked };
        chk.Checked += (_, _) => OnFieldEdited();
        chk.Unchecked += (_, _) => OnFieldEdited();
        return chk;
    }

    private static Button CreateButton(string label, RoutedEventHandler onClick, bool primary = false)
    {
        var btn = new Button
        {
            Content = label,
            Margin = new Thickness(4, 0, 0, 0),
            Background = new SolidColorBrush(primary ? Color.FromArgb(255, 145, 71, 255) : Color.FromArgb(255, 45, 45, 60)),
            Foreground = new SolidColorBrush(Colors.White),
        };
        btn.Click += onClick;
        return btn;
    }

    private static TextBlock CreateSectionHeader(string title) => new()
    {
        Text = title,
        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Color.FromArgb(255, 226, 232, 240)),
        Margin = new Thickness(0, 6, 0, 2),
    };

    private static Grid CreateTwoColRow(string label1, FrameworkElement ctrl1, string label2, FrameworkElement ctrl2)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var s1 = new StackPanel { Margin = new Thickness(0, 0, 6, 0), Spacing = 2 };
        s1.Children.Add(new TextBlock { Text = label1, FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(255, 160, 174, 192)) });
        s1.Children.Add(ctrl1);
        var s2 = new StackPanel { Margin = new Thickness(6, 0, 0, 0), Spacing = 2 };
        s2.Children.Add(new TextBlock { Text = label2, FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(255, 160, 174, 192)) });
        s2.Children.Add(ctrl2);
        Grid.SetColumn(s1, 0);
        Grid.SetColumn(s2, 1);
        g.Children.Add(s1);
        g.Children.Add(s2);
        return g;
    }
}
