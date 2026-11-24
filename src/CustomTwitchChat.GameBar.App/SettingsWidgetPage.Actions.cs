using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;
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

    private async void PasteChannelFromClipboard()
    {
        try
        {
            var view = Clipboard.GetContent();
            if (view.Contains(StandardDataFormats.Text))
            {
                var txt = (await view.GetTextAsync() ?? string.Empty).Trim();
                if (txt.StartsWith("#c=1.", StringComparison.Ordinal) ||
                    txt.StartsWith("{", StringComparison.Ordinal) ||
                    txt.Contains("overlay.html", StringComparison.OrdinalIgnoreCase))
                {
                    ImportRawText(txt);
                }
                else
                {
                    _channelBox.Text = txt;
                    ApplyToOverlay();
                }
            }
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Clipboard error: {ex.Message}";
        }
    }

    private async void PasteAndImportConfig()
    {
        try
        {
            var txt = (_importBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(txt))
            {
                var view = Clipboard.GetContent();
                if (view.Contains(StandardDataFormats.Text))
                {
                    txt = (await view.GetTextAsync() ?? string.Empty).Trim();
                    _importBox.Text = txt;
                }
            }
            if (!string.IsNullOrEmpty(txt)) ImportRawText(txt);
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Import failed: {ex.Message}";
        }
    }

    private void ImportRawText(string raw)
    {
        var snapshot = _hostApp.ConfigStore.ApplyInput(raw);
        _workingConfig = new Dictionary<string, object>(snapshot.Config, StringComparer.Ordinal);
        HydrateControlsFromConfig();
        PushConfigToPreview();
        _statusText.Text = $"Imported and applied (#{_workingConfig["channel"]}) — {snapshot.Hash}";
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
