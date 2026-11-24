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
    private UIElement BuildLayout()
    {
        var root = new Grid { Padding = new Thickness(12) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(430) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var formStack = new StackPanel { Spacing = 8, Padding = new Thickness(0, 0, 12, 12) };

        var chanRow = new Grid();
        chanRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        chanRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_channelBox, 0);
        var applyBtn = CreateButton("Apply to Overlay", (_, _) => ApplyToOverlay(), primary: true);
        Grid.SetColumn(applyBtn, 1);
        chanRow.Children.Add(_channelBox);
        chanRow.Children.Add(applyBtn);

        formStack.Children.Add(CreateSectionHeader("Twitch Channel"));
        formStack.Children.Add(chanRow);
        formStack.Children.Add(_statusText);

        formStack.Children.Add(CreateSectionHeader("Preset & Language"));
        formStack.Children.Add(CreateTwoColRow("Theme Preset", _presetCombo, "Language", _langCombo));

        formStack.Children.Add(CreateSectionHeader("Typography & Limits"));
        formStack.Children.Add(CreateTwoColRow("Font Family", _fontNameBox, "Font Size (px)", _fontSizeBox));
        formStack.Children.Add(CreateTwoColRow("Max Messages", _limitBox, "Hide After (s)", _hideAfterBox));
        formStack.Children.Add(CreateTwoColRow("Message Layout", _layoutCombo, "Avatar Mode", _avatarCombo));
        formStack.Children.Add(CreateTwoColRow("Username Color", _nickColorCombo, "Animation In", _animInCombo));

        formStack.Children.Add(CreateSectionHeader("Emotes & Twitch Events"));
        var checksGrid = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        checksGrid.Children.Add(_emote7tvCheck);
        checksGrid.Children.Add(_emoteBttvCheck);
        checksGrid.Children.Add(_emoteFfzCheck);
        formStack.Children.Add(checksGrid);

        var eventsGrid = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        eventsGrid.Children.Add(_subsCheck);
        eventsGrid.Children.Add(_cheersCheck);
        eventsGrid.Children.Add(_raidsCheck);
        eventsGrid.Children.Add(_streaksCheck);
        formStack.Children.Add(eventsGrid);

        formStack.Children.Add(CreateSectionHeader("Twitch Login (OAuth Device Flow)"));
        var authChecks = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        authChecks.Children.Add(_authEnabledCheck);
        authChecks.Children.Add(_authPointsCheck);
        authChecks.Children.Add(_authFollowsCheck);
        formStack.Children.Add(authChecks);

        var clientRow = new Grid();
        clientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        clientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        clientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        clientRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_clientIdBox, 0);
        var loginBtn = CreateButton("Login Twitch", (_, _) => StartTwitchDeviceLoginAsync(), primary: true);
        var devBtn = CreateButton("Dev Console", (_, _) => OpenTwitchDevConsoleAsync());
        var reloginBtn = CreateButton("Relogin", (_, _) => ForceRelogin());
        Grid.SetColumn(loginBtn, 1);
        Grid.SetColumn(devBtn, 2);
        Grid.SetColumn(reloginBtn, 3);
        clientRow.Children.Add(_clientIdBox);
        clientRow.Children.Add(loginBtn);
        clientRow.Children.Add(devBtn);
        clientRow.Children.Add(reloginBtn);
        formStack.Children.Add(clientRow);

        formStack.Children.Add(CreateSectionHeader("Import via PowerShell: .\\scripts\\apply-config.ps1 \"#c=1...\""));
        var importRow = new Grid();
        importRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        importRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_importBox, 0);
        var importBtn = CreateButton("Apply Input", (_, _) => PasteAndImportConfig());
        Grid.SetColumn(importBtn, 1);
        importRow.Children.Add(_importBox);
        importRow.Children.Add(importBtn);
        formStack.Children.Add(importRow);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = formStack,
        };
        Grid.SetColumn(scroll, 0);

        var previewPane = BuildPreviewPane();
        Grid.SetColumn(previewPane, 1);

        root.Children.Add(scroll);
        root.Children.Add(previewPane);
        return root;
    }

    private FrameworkElement BuildPreviewPane()
    {
        var grid = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 22, 22, 30)),
            Padding = new Thickness(10),
        };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var simBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 0, 8) };
        simBar.Children.Add(CreateButton("+ Message", (_, _) => TriggerSimulation("testMessage")));
        simBar.Children.Add(CreateButton("+ Emote", (_, _) => TriggerSimulation("testSolo")));
        simBar.Children.Add(CreateButton("+ Sub", (_, _) => TriggerSimulation("testSub")));
        simBar.Children.Add(CreateButton("+ Cheer", (_, _) => TriggerSimulation("testCheer")));
        simBar.Children.Add(CreateButton("+ Raid", (_, _) => TriggerSimulation("testRaid")));
        simBar.Children.Add(CreateButton("+ Streak", (_, _) => TriggerSimulation("testWatchStreak")));
        simBar.Children.Add(CreateButton("Clear", (_, _) => TriggerSimulation("testClear")));

        Grid.SetRow(simBar, 0);
        Grid.SetRow(_webView, 1);
        grid.Children.Add(simBar);
        grid.Children.Add(_webView);
        return grid;
    }

    private Grid BuildMouseKeypad()
    {
        var panel = new Grid
        {
            Visibility = Visibility.Visible,
            Background = new SolidColorBrush(Color.FromArgb(255, 24, 24, 34)),
            Padding = new Thickness(6),
        };
        var rows = new[] { "1234567890_", "qwertyuiop", "asdfghjkl", "zxcvbnm" };
        var stack = new StackPanel { Spacing = 4 };
        foreach (var r in rows)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
            foreach (var ch in r)
            {
                var s = ch.ToString();
                line.Children.Add(CreateKeyButton(s, () => _channelBox.Text += s));
            }
            if (r == "zxcvbnm")
            {
                line.Children.Add(CreateKeyButton("<-", () =>
                {
                    if (_channelBox.Text.Length > 0)
                        _channelBox.Text = _channelBox.Text[..^1];
                }));
                line.Children.Add(CreateKeyButton("Clear", () => _channelBox.Text = string.Empty));
                line.Children.Add(CreateKeyButton("Apply", ApplyToOverlay, primary: true));
            }
            stack.Children.Add(line);
        }
        panel.Children.Add(stack);
        return panel;
    }
}
