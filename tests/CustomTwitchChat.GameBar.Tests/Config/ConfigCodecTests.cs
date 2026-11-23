using System.Text.Json;
using CustomTwitchChat.GameBar.Core.Config;
using Xunit;

namespace CustomTwitchChat.GameBar.Tests.Config;

public sealed class ConfigCodecTests
{
    [Fact]
    public void SchemaCatalog_ContainsAllExpectedFieldsAndLookupKeys()
    {
        Assert.Equal(79, SchemaCatalog.Fields.Count);
        Assert.Equal(SchemaCatalog.Fields.Count, SchemaCatalog.ByKey.Count);
        Assert.True(SchemaCatalog.ByKey.ContainsKey("channel"));
        Assert.True(SchemaCatalog.ByKey.ContainsKey("userNameBackgroundColor"));
        Assert.True(SchemaCatalog.ByKey.ContainsKey("textBorderRepeat"));
        Assert.True(SchemaCatalog.ByKey.ContainsKey("messageSliceWidth"));
        Assert.True(SchemaCatalog.ByKey.ContainsKey("customCSS"));
    }

    [Fact]
    public void NormalizeJson_And_NormalizeConfig_ReturnDefaultsForInvalidInputs()
    {
        var defaults = ConfigCodec.GetDefaultConfig();
        Assert.Equal(defaults, ConfigCodec.NormalizeConfig(null));
        Assert.Equal(defaults, ConfigCodec.NormalizeObjectConfig(null));
        Assert.Equal(defaults, ConfigCodec.NormalizeJson(null));
        Assert.Equal(defaults, ConfigCodec.NormalizeJson("   "));
        Assert.Equal(defaults, ConfigCodec.NormalizeJson("[1,2,3]"));
        Assert.Equal(defaults, ConfigCodec.NormalizeJson("{broken-json"));
    }

    [Fact]
    public void NormalizeJson_CoercesLegacyQuirksClampsNumbersAndStripsUnknownKeys()
    {
        const string json = """
        {
          "channel": "  #@MyChannel  ",
          "textInSameLine": "no",
          "fontSize": "999",
          "avatarSize": -5,
          "messagesLimit": "not-a-number",
          "emoteSize": true,
          "nickColor": "invalid-option",
          "fontName": 123,
          "customCSS": { "bad": true },
          "hideAfter": null,
          "extraUnknownKey": "dropped"
        }
        """;

        var normalized = ConfigCodec.NormalizeJson(json);
        Assert.Equal("mychannel", normalized["channel"]);
        Assert.Equal("block", normalized["textInSameLine"]);
        Assert.Equal(96d, normalized["fontSize"]);
        Assert.Equal(12d, normalized["avatarSize"]);
        Assert.Equal(4d, normalized["messagesLimit"]);
        Assert.Equal(24d, normalized["emoteSize"]);
        Assert.Equal("user", normalized["nickColor"]);
        Assert.Equal("123", normalized["fontName"]);
        Assert.Equal("", normalized["customCSS"]);
        Assert.Equal(999d, normalized["hideAfter"]);
        Assert.False(normalized.ContainsKey("extraUnknownKey"));
    }

    [Fact]
    public void EncodeAndDecodeConfigHash_MatchesCustomTwitchChatDeltaFormat()
    {
        var defaults = ConfigCodec.GetDefaultConfig();
        Assert.Empty(ConfigCodec.DiffFromDefaults(defaults));
        Assert.Equal("#c=1.e30", ConfigCodec.EncodeConfigHash(defaults));

        var custom = new Dictionary<string, object>(defaults)
        {
            ["channel"] = "streamer",
            ["fontSize"] = 32.5d,
            ["emoteQuantity"] = 5d,
            ["lang"] = "en",
        };
        var hash = ConfigCodec.EncodeConfigHash(custom);

        Assert.Equal(custom, ConfigCodec.DecodeConfigHash(hash));
        Assert.Equal(custom, ConfigCodec.DecodeConfigHash(hash[3..]));
        Assert.Equal(custom, ConfigCodec.DecodeConfigHash($"foo=1&c={hash[3..]}"));
        Assert.Equal(custom, ConfigCodec.DecodeConfigHash($"https://example.com/overlay.html{hash}"));
        Assert.Equal(defaults, ConfigCodec.DecodeConfigHash(""));
        Assert.Equal(defaults, ConfigCodec.DecodeConfigHash("#c=2.abc"));
        Assert.Equal(defaults, ConfigCodec.DecodeConfigHash("#c=1.a"));
        Assert.Equal(defaults, ConfigCodec.DecodeConfigHash("#c=1.ab"));
        Assert.Equal(defaults, ConfigCodec.DecodeConfigHash("#c=1.abc"));
        Assert.Equal(defaults, ConfigCodec.DecodeConfigHash("#c=1.!!!bad!!!"));
    }

    [Fact]
    public void ImportConfigFromInput_HandlesHashesUrlsHtmlAndStreamElementsJson()
    {
        var custom = ConfigCodec.GetDefaultConfig();
        custom["channel"] = "levy";
        custom["fontSize"] = 36d;
        var hash = ConfigCodec.EncodeConfigHash(custom);

        Assert.Equal(custom, ConfigCodec.ImportConfigFromInput(hash));
        Assert.Equal(custom, ConfigCodec.ImportConfigFromInput($"c={hash[3..]}"));
        Assert.Equal(custom, ConfigCodec.ImportConfigFromInput(hash[3..]));
        Assert.Equal(custom, ConfigCodec.ImportConfigFromInput($"https://example.github.io/custom-twitch-chat/overlay.html{hash}"));

        const string html = "<script>window.__CTC_CONFIG__ = {\"channel\":\"HTMLChan\",\"fontSize\":28};</script>";
        var fromHtml = ConfigCodec.ImportConfigFromInput(html);
        Assert.Equal("htmlchan", fromHtml["channel"]);
        Assert.Equal(28d, fromHtml["fontSize"]);

        const string seJson = "{\"fontName\":\"Permanent Marker\",\"textInSameLine\":\"no\",\"emoteSoloSize\":\"120\"}";
        var fromSe = ConfigCodec.ImportConfigFromInput(seJson);
        Assert.Equal("Permanent Marker", fromSe["fontName"]);
        Assert.Equal("block", fromSe["textInSameLine"]);
        Assert.Equal(120d, fromSe["emoteSoloSize"]);

        Assert.Throws<FormatException>(() => ConfigCodec.ImportConfigFromInput("   "));
        Assert.Throws<FormatException>(() => ConfigCodec.ImportConfigFromInput("[1,2,3]"));
    }

    [Fact]
    public void BuildOverlayUri_ReplacesExistingFragmentWithEncodedHash()
    {
        var cfg = ConfigCodec.GetDefaultConfig();
        cfg["channel"] = "demo";
        var expectedHash = ConfigCodec.EncodeConfigHash(cfg);

        Assert.Equal(
            $"ms-appx-web:///WebAssets/overlay.html{expectedHash}",
            ConfigCodec.BuildOverlayUri("ms-appx-web:///WebAssets/overlay.html#c=1.old", cfg));
        Assert.Equal(
            expectedHash,
            ConfigCodec.BuildOverlayUri(string.Empty, cfg));
    }
}
