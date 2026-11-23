using System.Text.Json;
using CustomTwitchChat.GameBar.Core.State;

namespace CustomTwitchChat.GameBar.Core.Bridge;

/// <summary>
/// Result of processing a JSON message envelope from an embedded WebView.
/// </summary>
public sealed record BridgeResult(
    bool Handled,
    string? ResponseJson,
    string? ErrorCode,
    string? BroadcastJson = null);

/// <summary>
/// Bidirectional JSON message contract between WebView pages and the Game Bar widget host.
/// </summary>
public sealed class BridgeProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ConfigSyncStore _store;
    private readonly WidgetStateController _stateController;

    public BridgeProtocol(ConfigSyncStore store, WidgetStateController stateController)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _stateController = stateController ?? throw new ArgumentNullException(nameof(stateController));
    }

    public BridgeResult HandleIncomingMessage(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return new BridgeResult(false, null, "invalid_message");

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("type", out var typeProp) ||
                typeProp.ValueKind != JsonValueKind.String)
            {
                return new BridgeResult(false, null, "invalid_message");
            }

            return typeProp.GetString() switch
            {
                "config:apply" => HandleConfigApply(doc.RootElement),
                "state:query" => HandleStateQuery(),
                "settings:open" => HandleSettingsOpen(),
                _ => new BridgeResult(false, null, "unsupported_type"),
            };
        }
        catch (JsonException)
        {
            return new BridgeResult(false, null, "invalid_message");
        }
    }

    public string CreateStateEnvelope(OverlayVisualState state, ConfigSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(new
        {
            type = "state:update",
            state,
            snapshot.Hash,
            snapshot.OverlayUri,
            snapshot.Config,
        }, JsonOptions);
    }

    public string CreateConfigEnvelope(ConfigSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(new
        {
            type = "config:updated",
            snapshot.Config,
            snapshot.Hash,
            snapshot.OverlayUri,
        }, JsonOptions);
    }

    private BridgeResult HandleConfigApply(JsonElement root)
    {
        try
        {
            ConfigSnapshot snapshot;
            if (root.TryGetProperty("config", out var configEl) && configEl.ValueKind == JsonValueKind.Object)
            {
                snapshot = _store.ApplyJsonElement(configEl);
            }
            else if (TryGetNonEmptyString(root, "input", out var input) || TryGetNonEmptyString(root, "hash", out input))
            {
                snapshot = _store.ApplyInput(input);
            }
            else
            {
                return new BridgeResult(false, null, "invalid_config");
            }

            var envelope = CreateConfigEnvelope(snapshot);
            return new BridgeResult(true, envelope, null, envelope);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return new BridgeResult(false, null, "invalid_config");
        }
    }

    private BridgeResult HandleStateQuery()
    {
        var envelope = CreateStateEnvelope(_stateController.CurrentState, _store.GetSnapshot());
        return new BridgeResult(true, envelope, null);
    }

    private BridgeResult HandleSettingsOpen()
    {
        _stateController.RequestOpenSettings();
        return new BridgeResult(true, null, null);
    }

    private static bool TryGetNonEmptyString(JsonElement root, string name, out string value)
    {
        if (root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var raw = prop.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                value = raw;
                return true;
            }
        }
        value = string.Empty;
        return false;
    }
}
