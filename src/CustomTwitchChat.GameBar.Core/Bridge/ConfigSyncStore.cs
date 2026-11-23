using System.Text.Json;
using CustomTwitchChat.GameBar.Core.Config;

namespace CustomTwitchChat.GameBar.Core.Bridge;

/// <summary>
/// Persistence abstraction for storing normalized widget configurations.
/// </summary>
public interface IConfigStorage
{
    string? LoadRaw();
    void SaveRaw(string json);
}

/// <summary>
/// Thread-safe in-memory storage used for testing and ephemeral sessions.
/// </summary>
public sealed class InMemoryConfigStorage : IConfigStorage
{
    private string? _payload;
    public string? LoadRaw() => _payload;
    public void SaveRaw(string json) => _payload = json;
}

/// <summary>
/// File-backed atomic storage for persisted widget configurations.
/// </summary>
public sealed class FileConfigStorage : IConfigStorage
{
    private readonly string _filePath;

    public FileConfigStorage(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));
        _filePath = filePath;
    }

    public string? LoadRaw() => File.Exists(_filePath) ? File.ReadAllText(_filePath) : null;

    public void SaveRaw(string json)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _filePath, overwrite: true);
    }
}

/// <summary>
/// Immutable snapshot of the active configuration, its #c=1.&lt;base64url&gt; hash, and overlay URI.
/// </summary>
public sealed record ConfigSnapshot(
    IReadOnlyDictionary<string, object> Config,
    string Hash,
    string OverlayUri);

/// <summary>
/// Synchronizes and persists custom-twitch-chat configs shared across Settings and Overlay views.
/// </summary>
public sealed class ConfigSyncStore
{
    private readonly object _sync = new();
    private readonly IConfigStorage _storage;
    private readonly string _baseOverlayUri;
    private Dictionary<string, object> _currentConfig;

    public ConfigSyncStore(
        IConfigStorage? storage = null,
        string baseOverlayUri = "ms-appx-web:///WebAssets/overlay.html")
    {
        _storage = storage ?? new InMemoryConfigStorage();
        _baseOverlayUri = baseOverlayUri;
        _currentConfig = LoadInitial();
    }

    public string? LastPersistenceError { get; private set; }

    public event Action<ConfigSnapshot>? ConfigChanged;

    public ConfigSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return BuildSnapshot(_currentConfig);
        }
    }

    public ConfigSnapshot ApplyConfig(IReadOnlyDictionary<string, object?> raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var normalized = ConfigCodec.NormalizeObjectConfig(raw);
        return Commit(normalized);
    }

    public ConfigSnapshot ApplyInput(string rawInput)
    {
        var normalized = ConfigCodec.ImportConfigFromInput(rawInput);
        return Commit(normalized);
    }

    public ConfigSnapshot ApplyJsonElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Config payload must be a JSON object.");
        }
        var normalized = ConfigCodec.NormalizeJson(element.GetRawText());
        return Commit(normalized);
    }

    private Dictionary<string, object> LoadInitial()
    {
        try
        {
            var raw = _storage.LoadRaw();
            LastPersistenceError = null;
            return string.IsNullOrWhiteSpace(raw)
                ? ConfigCodec.GetDefaultConfig()
                : ConfigCodec.NormalizeJson(raw);
        }
        catch (Exception ex)
        {
            LastPersistenceError = ex.Message;
            return ConfigCodec.GetDefaultConfig();
        }
    }

    private ConfigSnapshot Commit(Dictionary<string, object> normalized)
    {
        ConfigSnapshot snapshot;
        lock (_sync)
        {
            _currentConfig = normalized;
            TryPersist(normalized);
            snapshot = BuildSnapshot(normalized);
        }
        ConfigChanged?.Invoke(snapshot);
        return snapshot;
    }

    private void TryPersist(Dictionary<string, object> normalized)
    {
        try
        {
            _storage.SaveRaw(JsonSerializer.Serialize(normalized));
            LastPersistenceError = null;
        }
        catch (Exception ex)
        {
            LastPersistenceError = ex.Message;
        }
    }

    private ConfigSnapshot BuildSnapshot(Dictionary<string, object> config)
    {
        var copy = new Dictionary<string, object>(config, StringComparer.Ordinal);
        var hash = ConfigCodec.EncodeConfigHash(copy);
        var uri = ConfigCodec.BuildOverlayUri(_baseOverlayUri, copy);
        return new ConfigSnapshot(copy, hash, uri);
    }
}
