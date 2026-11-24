using System.Globalization;
using System.Text.Json;

namespace CustomTwitchChat.GameBar.Core.Config;

/// <summary>
/// Normalizes, diffs, imports, and encodes custom-twitch-chat configurations.
/// </summary>
public static partial class ConfigCodec
{
    public static Dictionary<string, object> GetDefaultConfig()
    {
        var result = new Dictionary<string, object>(SchemaCatalog.Fields.Count, StringComparer.Ordinal);
        foreach (var field in SchemaCatalog.Fields)
        {
            result[field.Key] = field.DefaultValue;
        }
        return result;
    }

    public static Dictionary<string, object> NormalizeConfig(IReadOnlyDictionary<string, JsonElement>? raw)
    {
        var result = GetDefaultConfig();
        if (raw is null) return result;
        foreach (var field in SchemaCatalog.Fields)
        {
            if (raw.TryGetValue(field.Key, out var value))
            {
                result[field.Key] = CoerceJsonValue(field, value);
            }
        }
        return result;
    }

    public static Dictionary<string, object> NormalizeJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return GetDefaultConfig();
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? NormalizeElement(doc.RootElement)
                : GetDefaultConfig();
        }
        catch (JsonException)
        {
            return GetDefaultConfig();
        }
    }

    public static Dictionary<string, object> NormalizeObjectConfig(IReadOnlyDictionary<string, object?>? raw)
    {
        if (raw is null) return GetDefaultConfig();
        return NormalizeJson(JsonSerializer.Serialize(raw));
    }

    public static Dictionary<string, object> DiffFromDefaults(IReadOnlyDictionary<string, object> config)
    {
        var normalized = NormalizeObjectConfig(config.ToDictionary(kvp => kvp.Key, object? (kvp) => kvp.Value));
        var defaults = GetDefaultConfig();
        var delta = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var field in SchemaCatalog.Fields)
        {
            var value = normalized[field.Key];
            if (!AreEqual(value, defaults[field.Key]))
            {
                delta[field.Key] = ToSerializableValue(value);
            }
        }
        return delta;
    }

    private static Dictionary<string, object> NormalizeElement(JsonElement root)
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in root.EnumerateObject())
        {
            dict[prop.Name] = prop.Value;
        }
        return NormalizeConfig(dict);
    }

    private static object CoerceJsonValue(SchemaField field, JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return field.DefaultValue;
        if (field.Key == "textInSameLine" && value.ValueKind == JsonValueKind.String && value.GetString() == "no")
            return "block";
        if (field.Type == "number") return CoerceNumber(field, value);
        if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number)) return field.DefaultValue;

        var str = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
        if (field.Type == "select" && field.Options is not null)
            return field.Options.Contains(str, StringComparer.Ordinal) ? str : field.DefaultValue;

        return field.Key == "channel" ? str.Trim().TrimStart('#', '@').Trim().ToLowerInvariant() : str;
    }

    private static double CoerceNumber(SchemaField field, JsonElement value)
    {
        double num;
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetDouble(out num) || !double.IsFinite(num)) return (double)field.DefaultValue;
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            var s = value.GetString();
            if (string.IsNullOrWhiteSpace(s) ||
                !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out num) ||
                !double.IsFinite(num))
                return (double)field.DefaultValue;
        }
        else
        {
            return (double)field.DefaultValue;
        }

        if (field.Min.HasValue) num = Math.Max(field.Min.Value, num);
        if (field.Max.HasValue) num = Math.Min(field.Max.Value, num);
        return num;
    }

    private static bool AreEqual(object a, object b) =>
        a is double da && b is double db ? Math.Abs(da - db) < 1e-9 : Equals(a, b);

    private static object ToSerializableValue(object value) =>
        value is double d && double.IsFinite(d) && d == Math.Truncate(d) && d >= long.MinValue && d <= long.MaxValue
            ? (long)d
            : value;
}
