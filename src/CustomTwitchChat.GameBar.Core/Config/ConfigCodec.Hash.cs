using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CustomTwitchChat.GameBar.Core.Config;

public static partial class ConfigCodec
{
    private static readonly Regex HashParamPattern = new(@"(?:^|&)c=([^&]+)", RegexOptions.Compiled);
    private static readonly Regex InputHashPattern = new(@"#?(?:c=)?(1\.[A-Za-z0-9_-]+)", RegexOptions.Compiled);
    private static readonly Regex HtmlConfigPattern = new(@"window\.__CTC_CONFIG__\s*=\s*(\{[\s\S]*?\})\s*;", RegexOptions.Compiled);

    public static string EncodeConfigHash(IReadOnlyDictionary<string, object> config)
    {
        var delta = DiffFromDefaults(config);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(delta);
        return $"#c=1.{ToBase64Url(bytes)}";
    }

    public static Dictionary<string, object> DecodeConfigHash(string? hashOrUrl)
    {
        if (string.IsNullOrWhiteSpace(hashOrUrl)) return GetDefaultConfig();
        var trimmed = hashOrUrl.Trim();
        var hashIdx = trimmed.IndexOf('#');
        var clean = hashIdx >= 0 ? trimmed[(hashIdx + 1)..] : trimmed;
        var match = HashParamPattern.Match(clean);
        var rawToken = match.Success ? match.Groups[1].Value : clean;
        if (!rawToken.StartsWith("1.", StringComparison.Ordinal)) return GetDefaultConfig();

        try
        {
            var json = Encoding.UTF8.GetString(FromBase64Url(rawToken[2..]));
            return NormalizeJson(json);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return GetDefaultConfig();
        }
    }

    public static Dictionary<string, object> ImportConfigFromInput(string? rawInput)
    {
        var text = (rawInput ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text)) throw new FormatException("Empty configuration input.");

        var hashMatch = InputHashPattern.Match(text);
        if (hashMatch.Success && (
            text.Contains("overlay.html", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith('#') ||
            text.StartsWith("c=", StringComparison.Ordinal) ||
            text.StartsWith("1.", StringComparison.Ordinal)))
        {
            return DecodeConfigHash($"#c={hashMatch.Groups[1].Value}");
        }

        var htmlMatch = HtmlConfigPattern.Match(text);
        if (htmlMatch.Success)
        {
            using var htmlDoc = JsonDocument.Parse(htmlMatch.Groups[1].Value);
            return NormalizeElement(htmlDoc.RootElement);
        }

        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Configuration must be a JSON object.");
        }
        return NormalizeElement(doc.RootElement);
    }

    public static string BuildOverlayUri(string baseOverlayUri, IReadOnlyDictionary<string, object> config)
    {
        var cleanBase = baseOverlayUri ?? string.Empty;
        var hashIndex = cleanBase.IndexOf('#');
        if (hashIndex >= 0) cleanBase = cleanBase[..hashIndex];
        return $"{cleanBase}{EncodeConfigHash(config)}";
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] FromBase64Url(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        var mod = s.Length % 4;
        if (mod == 2) s += "==";
        else if (mod == 3) s += "=";
        else if (mod == 1) throw new FormatException("Invalid Base64URL length.");
        return Convert.FromBase64String(s);
    }
}
