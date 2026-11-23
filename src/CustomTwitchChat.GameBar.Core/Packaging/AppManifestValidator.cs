using System.Xml;
using System.Xml.Linq;

namespace CustomTwitchChat.GameBar.Core.Packaging;

/// <summary>
/// Result of validating an AppxManifest against the Xbox Game Bar widget contract.
/// </summary>
public sealed record ManifestValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> WidgetIds);

/// <summary>
/// Validates Package.appxmanifest declarations for Xbox Game Bar widget extensions.
/// </summary>
public static class AppManifestValidator
{
    public const string ExtensionName = "microsoft.gameBarUIExtension";
    public const string OverlayWidgetId = "CustomTwitchChat.Overlay";
    public const string SettingsWidgetId = "CustomTwitchChat.Settings";

    public static ManifestValidationResult ValidateFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new ManifestValidationResult(false, ["Manifest file does not exist."], []);
        }
        return ValidateXml(File.ReadAllText(filePath));
    }

    public static ManifestValidationResult ValidateXml(string? xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            return new ManifestValidationResult(false, ["Manifest XML is empty."], []);
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlContent);
        }
        catch (XmlException ex)
        {
            return new ManifestValidationResult(false, [$"Invalid XML: {ex.Message}"], []);
        }

        var errors = new List<string>();
        var root = doc.Root;
        if (root is null || root.Name.LocalName != "Package")
        {
            return new ManifestValidationResult(false, ["Root element must be <Package>."], []);
        }

        var hasInternet = root.Descendants()
            .Any(e => e.Name.LocalName == "Capability" &&
                      string.Equals(e.Attribute("Name")?.Value, "internetClient", StringComparison.Ordinal));
        if (!hasInternet) errors.Add("Missing <Capability Name=\"internetClient\" /> declaration.");

        var extensions = root.Descendants()
            .Where(e => e.Name.LocalName == "AppExtension" &&
                        string.Equals(e.Attribute("Name")?.Value, ExtensionName, StringComparison.Ordinal))
            .ToList();

        var widgetIds = extensions
            .Select(e => e.Attribute("Id")?.Value ?? string.Empty)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        ValidateOverlay(extensions, errors);
        ValidateSettings(extensions, errors);

        return new ManifestValidationResult(errors.Count == 0, errors, widgetIds);
    }

    private static void ValidateOverlay(IReadOnlyList<XElement> extensions, List<string> errors)
    {
        var overlay = extensions.FirstOrDefault(e => e.Attribute("Id")?.Value == OverlayWidgetId);
        if (overlay is null)
        {
            errors.Add($"Missing {ExtensionName} extension with Id=\"{OverlayWidgetId}\".");
            return;
        }

        var widget = overlay.Descendants().FirstOrDefault(e => e.Name.LocalName == "GameBarWidget");
        if (widget is null || widget.Attribute("Type")?.Value != "Standard")
        {
            errors.Add($"{OverlayWidgetId} must declare <GameBarWidget Type=\"Standard\">.");
            return;
        }

        RequireTrueElement(widget, "AllowForegroundTransparency", errors, OverlayWidgetId);
        RequireTrueElement(widget, "PinningSupported", errors, OverlayWidgetId);
        RequireTrueElement(widget, "ClickThroughSupported", errors, OverlayWidgetId);

        var settingsNode = widget.Descendants().FirstOrDefault(e => e.Name.LocalName == "SettingsSupported");
        if (settingsNode?.Attribute("AppExtensionId")?.Value != SettingsWidgetId)
        {
            errors.Add($"{OverlayWidgetId} must declare <SettingsSupported AppExtensionId=\"{SettingsWidgetId}\" />.");
        }
    }

    private static void ValidateSettings(IReadOnlyList<XElement> extensions, List<string> errors)
    {
        var settings = extensions.FirstOrDefault(e => e.Attribute("Id")?.Value == SettingsWidgetId);
        if (settings is null)
        {
            errors.Add($"Missing {ExtensionName} extension with Id=\"{SettingsWidgetId}\".");
            return;
        }

        var widget = settings.Descendants().FirstOrDefault(e => e.Name.LocalName == "GameBarWidget");
        if (widget is null || widget.Attribute("Type")?.Value != "Settings")
        {
            errors.Add($"{SettingsWidgetId} must declare <GameBarWidget Type=\"Settings\">.");
        }
    }

    private static void RequireTrueElement(XElement widget, string elementName, List<string> errors, string id)
    {
        var value = widget.Descendants().FirstOrDefault(e => e.Name.LocalName == elementName)?.Value.Trim();
        if (!string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{id} must enable <{elementName}>true</{elementName}>.");
        }
    }
}
