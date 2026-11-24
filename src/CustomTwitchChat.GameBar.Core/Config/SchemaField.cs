namespace CustomTwitchChat.GameBar.Core.Config;

/// <summary>
/// Describes a configuration field matching the custom-twitch-chat schema.
/// </summary>
public sealed record SchemaField(
    string Key,
    string Type,
    string Group,
    object DefaultValue,
    double? Min = null,
    double? Max = null,
    IReadOnlyList<string>? Options = null);
