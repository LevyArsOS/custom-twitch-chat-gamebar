using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml.Controls;

namespace CustomTwitchChat.GameBar.App;

public sealed partial class SettingsWidgetPage
{
    private readonly TextBox _fontNameBox;
    private readonly TextBox _fontSizeBox;
    private readonly TextBox _limitBox;
    private readonly TextBox _hideAfterBox;
    private readonly TextBox _importBox;
    private readonly ComboBox _presetCombo;
    private readonly ComboBox _langCombo;
    private readonly ComboBox _layoutCombo;
    private readonly ComboBox _avatarCombo;
    private readonly ComboBox _nickColorCombo;
    private readonly ComboBox _animInCombo;
    private readonly CheckBox _emote7tvCheck;
    private readonly CheckBox _emoteBttvCheck;
    private readonly CheckBox _emoteFfzCheck;
    private readonly CheckBox _subsCheck;
    private readonly CheckBox _cheersCheck;
    private readonly CheckBox _raidsCheck;
    private readonly CheckBox _streaksCheck;
    private readonly CheckBox _authEnabledCheck;
    private readonly CheckBox _authPointsCheck;
    private readonly CheckBox _authFollowsCheck;
    private readonly TextBox _clientIdBox;

    private void OnFieldEdited()
    {
        if (_hydrating) return;
        ReadControlsIntoWorkingConfig();
        PushConfigToPreview();
    }

    private void ApplyToOverlay()
    {
        ReadControlsIntoWorkingConfig();
        var nullableMap = _workingConfig.ToDictionary(k => k.Key, object? (v) => v.Value, StringComparer.Ordinal);
        var snapshot = _hostApp.ConfigStore.ApplyConfig(nullableMap);
        _workingConfig = new Dictionary<string, object>(snapshot.Config, StringComparer.Ordinal);
        HydrateControlsFromConfig();
        PushConfigToPreview();
        _statusText.Text = $"Applied to overlay (#{_workingConfig["channel"]}) — {snapshot.Hash}";
    }

    private void ForceRelogin()
    {
        var currentNonce = _workingConfig.TryGetValue("authNonce", out var n) && n is double d ? d : 1d;
        _workingConfig["authNonce"] = currentNonce + 1d;
        _authEnabledCheck.IsChecked = true;
        ApplyToOverlay();
    }

    private async void PasteClientIdFromClipboard()
    {
        var txt = await ReadClipboardTextAsync();
        if (txt is null) return;
        RunOnUi(() =>
        {
            _clientIdBox.Text = txt;
            _authEnabledCheck.IsChecked = true;
            ApplyToOverlay();
        });
    }

    private async void PasteChannelFromClipboard()
    {
        var txt = await ReadClipboardTextAsync();
        if (txt is null) return;
        if (txt.StartsWith("#c=1.", StringComparison.Ordinal) ||
            txt.StartsWith("{", StringComparison.Ordinal) ||
            txt.Contains("overlay.html", StringComparison.OrdinalIgnoreCase))
        {
            ImportRawText(txt);
        }
        else
        {
            RunOnUi(() =>
            {
                _channelBox.Text = txt;
                ApplyToOverlay();
            });
        }
    }

    private async void OpenFullConfiguratorAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("ctc-gamebar://configure"));
    }

    private async void OpenWebConfiguratorAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://levyarsos.github.io/custom-twitch-chat/"));
        SetStatus("Opened web configurator. Copy the OBS URL (#c=1...) or export chat.data.json and import it here.");
    }

    private async void LoadFileConfigAsync()
    {
        try
        {
            App.Trace("LoadFileConfigAsync opening FileOpenPicker");
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.Downloads,
                ViewMode = PickerViewMode.List,
            };
            picker.FileTypeFilter.Add(".json");
            picker.FileTypeFilter.Add(".html");
            picker.FileTypeFilter.Add(".htm");
            picker.FileTypeFilter.Add(".txt");
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                App.Trace("LoadFileConfigAsync cancelled");
                return;
            }
            App.Trace($"LoadFileConfigAsync picked {file.Name}");
            var content = await FileIO.ReadTextAsync(file);
            if (!string.IsNullOrWhiteSpace(content)) ImportRawText(content);
        }
        catch (Exception ex)
        {
            App.Trace($"LoadFileConfigAsync ERROR: {ex}");
            SetStatus($"File import failed: {ex.Message}");
        }
    }

    private async void PasteAndImportConfig()
    {
        try
        {
            var txt = (_importBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(txt))
            {
                txt = await ReadClipboardTextAsync() ?? string.Empty;
                if (!string.IsNullOrEmpty(txt)) RunOnUi(() => _importBox.Text = txt);
            }
            if (!string.IsNullOrEmpty(txt)) ImportRawText(txt);
        }
        catch (Exception ex)
        {
            App.Trace($"PasteAndImportConfig ERROR: {ex}");
            SetStatus($"Import failed: {ex.Message}");
        }
    }

    private async Task<string?> ReadClipboardTextAsync()
    {
        try
        {
            var view = Clipboard.GetContent();
            if (view.Contains(StandardDataFormats.Text))
            {
                return (await view.GetTextAsync() ?? string.Empty).Trim();
            }
        }
        catch
        {
            // UWP Clipboard.GetContent() can throw UnauthorizedAccessException inside Game Bar overlay.
        }

        var nativeText = TryReadWin32ClipboardText();
        if (nativeText is not null) return nativeText.Trim();

        SetStatus("Clipboard empty or unavailable; click the box and press Ctrl+V.");
        return null;
    }

    private static string? TryReadWin32ClipboardText()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            var handle = GetClipboardData(13); // CF_UNICODETEXT
            if (handle == IntPtr.Zero) return null;
            var ptr = GlobalLock(handle);
            if (ptr == IntPtr.Zero) return null;
            try
            {
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    private void ImportRawText(string raw)
    {
        try
        {
            App.Trace($"ImportRawText length={raw.Length}");
            var snapshot = _hostApp.ConfigStore.ApplyInput(raw);
            RunOnUi(() =>
            {
                _workingConfig = new Dictionary<string, object>(snapshot.Config, StringComparer.Ordinal);
                HydrateControlsFromConfig();
                PushConfigToPreview();
                _statusText.Text = $"Imported and applied (#{_workingConfig["channel"]}) — {snapshot.Hash}";
            });
        }
        catch (Exception ex)
        {
            App.Trace($"ImportRawText ERROR: {ex}");
            SetStatus($"Import error: {ex.Message}");
        }
    }

    private void PushConfigToPreview()
    {
        var json = JsonSerializer.Serialize(new { type = "ctc:config", config = _workingConfig });
        _ = DispatchScriptAsync($"window.postMessage({json}, '*');");
    }

    private void TriggerSimulation(string action)
    {
        var json = JsonSerializer.Serialize(new { type = "ctc:sim", action });
        _ = DispatchScriptAsync($"window.postMessage({json}, '*');");
    }
}
