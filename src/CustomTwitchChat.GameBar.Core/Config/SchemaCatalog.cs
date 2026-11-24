namespace CustomTwitchChat.GameBar.Core.Config;

/// <summary>
/// Authoritative catalog of all configuration fields supported by custom-twitch-chat.
/// </summary>
public static class SchemaCatalog
{
    private static readonly string[] YesNo = ["yes", "no"];
    private static readonly string[] RepeatModes = ["stretch", "repeat", "round", "space"];

    private static readonly string[] AnimationsIn =
    [
        "none", "bounceIn", "bounceInDown", "bounceInLeft", "bounceInRight", "bounceInUp",
        "fadeIn", "fadeInDown", "fadeInDownBig", "fadeInLeft", "fadeInLeftBig",
        "fadeInRight", "fadeInRightBig", "fadeInUp", "fadeInUpBig", "flipInX", "flipInY",
        "lightSpeedIn", "rotateIn", "rotateInDownLeft", "rotateInDownRight",
        "rotateInUpLeft", "rotateInUpRight", "slideInUp", "slideInDown",
        "slideInLeft", "slideInRight", "zoomIn", "zoomInDown", "zoomInLeft",
        "zoomInRight", "zoomInUp", "jackInTheBox", "rollIn",
    ];

    private static readonly string[] AnimationsOut =
    [
        "none", "bounceOut", "bounceOutDown", "bounceOutLeft", "bounceOutRight", "bounceOutUp",
        "fadeOut", "fadeOutDown", "fadeOutDownBig", "fadeOutLeft", "fadeOutLeftBig",
        "fadeOutRight", "fadeOutRightBig", "fadeOutUp", "fadeOutUpBig", "flipOutX", "flipOutY",
        "lightSpeedOut", "rotateOut", "rotateOutDownLeft", "rotateOutDownRight",
        "rotateOutUpLeft", "rotateOutUpRight", "slideOutUp", "slideOutDown",
        "slideOutLeft", "slideOutRight", "zoomOut", "zoomOutDown", "zoomOutLeft",
        "zoomOutRight", "zoomOutUp", "rollOut",
    ];

    public static IReadOnlyList<SchemaField> Fields { get; } = BuildFields();

    public static IReadOnlyDictionary<string, SchemaField> ByKey { get; } =
        Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);

    private static SchemaField F(
        string key, string type, string group, object def,
        double? min = null, double? max = null, IReadOnlyList<string>? options = null) =>
        new(key, type, group, def, min, max, options);

    private static SchemaField Toggle(string key, string group, string def = "yes") =>
        F(key, "select", group, def, options: YesNo);

    private static IEnumerable<SchemaField> Box(string prefix, string group) =>
    [
        F($"{prefix}BackgroundColor", "color", group, "rgba(0,0,0,0)"),
        F($"{prefix}BackgroundImage", "text", group, ""),
        F($"{prefix}BorderColor", "color", group, "rgba(0,0,0,0)"),
        F($"{prefix}BorderRadius", "number", group, 20d, 0d, 200d),
        F($"{prefix}BorderWidth", "number", group, 4d, 0d, 64d),
        F($"{prefix}BorderImage", "text", group, ""),
        F($"{prefix}SliceWidth", "number", group, 32d, 0d, 256d),
        F($"{prefix}BorderRepeat", "select", group, "stretch", options: RepeatModes),
    ];

    private static IReadOnlyList<SchemaField> BuildFields()
    {
        var list = new List<SchemaField>
        {
            F("channel", "text", "channel", ""),
            F("lang", "select", "channel", "pt-BR", options: ["pt-BR", "en"]),
            F("fontName", "text", "typography", "Montserrat"),
            F("fontSize", "number", "typography", 24d, 8d, 96d),
            F("fontWeight", "select", "typography", "400", options: ["100", "300", "400", "500", "700", "900"]),
            F("fontColor", "color", "typography", "rgba(255,255,255,1)"),
            F("textShadow", "text", "typography", "rgb(0, 0, 0) 1px 1px 1px"),
            F("emoteQuantity", "number", "typography", 3d, 0d, 20d),
            F("emoteSize", "number", "typography", 24d, 12d, 128d),
            F("emoteSoloSize", "number", "typography", 48d, 16d, 256d),
            F("alignMessages", "select", "general", "flex", options: ["flex", "block"]),
            F("messagesLimit", "number", "general", 4d, 1d, 100d),
            F("hideAfter", "number", "general", 999d, 1d, 999d),
            Toggle("hideCommands", "general", "yes"),
            Toggle("mergeMessages", "general", "no"),
            F("ignoredUsers", "text", "general", "StreamElements,Nightbot"),
            F("blockedWords", "text", "general", ""),
            F("animationIn", "select", "general", "none", options: AnimationsIn),
            F("animationOut", "select", "general", "none", options: AnimationsOut),
            F("showAvatar", "select", "profile", "inline", options: ["block", "inline", "none"]),
            F("avatarSize", "number", "profile", 36d, 12d, 128d),
            F("avatarRound", "select", "profile", "50", options: ["0", "25", "50"]),
            F("nickColor", "select", "profile", "user", options: ["user", "messagecolor", "custom", "remove"]),
            F("customNickColor", "color", "profile", "rgba(0,255,0,1)"),
            F("displayBadges", "select", "profile", "inline", options: ["inline", "none"]),
            F("nameSuffix", "text", "profile", ":"),
        };
        list.AddRange(Box("userName", "profile"));
        list.Add(F("textInSameLine", "select", "text", "block", options: ["inline", "block"]));
        list.Add(Toggle("colorMentions", "text", "yes"));
        list.AddRange(Box("text", "text"));
        list.AddRange(Box("message", "block"));
        list.AddRange([
            Toggle("enable7tv", "emotes", "yes"),
            Toggle("enableBttv", "emotes", "yes"),
            Toggle("enableFfz", "emotes", "yes"),
            Toggle("enableTwemoji", "emotes", "yes"),
            Toggle("showSubs", "events", "yes"),
            F("subColor", "color", "events", "rgba(255,159,28,1)"),
            Toggle("showCheers", "events", "yes"),
            F("cheerColor", "color", "events", "rgba(0,229,255,1)"),
            Toggle("showPoints", "events", "yes"),
            F("pointsColor", "color", "events", "rgba(46,196,182,1)"),
            Toggle("showRaids", "events", "yes"),
            F("raidColor", "color", "events", "rgba(255,77,109,1)"),
            Toggle("showStreaks", "events", "yes"),
            F("streakColor", "color", "events", "rgba(145,71,255,1)"),
            Toggle("showAnnouncements", "events", "yes"),
            Toggle("showFollows", "events", "yes"),
            F("followColor", "color", "events", "rgba(128,237,153,1)"),
            Toggle("showHighlights", "events", "yes"),
            F("highlightColor", "color", "events", "rgba(117,94,188,0.35)"),
            Toggle("showFirstMsg", "events", "yes"),
            F("firstMsgColor", "color", "events", "rgba(145,71,255,0.45)"),
            Toggle("authEnabled", "oauth", "no"),
            Toggle("authPoints", "oauth", "no"),
            Toggle("authFollows", "oauth", "no"),
            F("clientId", "text", "oauth", ""),
            F("authNonce", "number", "oauth", 1d, 1d, 999999d),
            F("customCSS", "textarea", "advanced", ""),
        ]);
        return list;
    }
}
