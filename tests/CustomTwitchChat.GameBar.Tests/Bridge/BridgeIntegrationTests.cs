using System.Text.Json;
using CustomTwitchChat.GameBar.Core.Bridge;
using CustomTwitchChat.GameBar.Core.Config;
using CustomTwitchChat.GameBar.Core.State;
using Xunit;

namespace CustomTwitchChat.GameBar.Tests.Bridge;

public sealed class BridgeIntegrationTests
{
    [Fact]
    public void SettingsApply_BroadcastsNormalizedConfigAndHashToOverlayAndPersistsToFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ctc-gb-{Guid.NewGuid():N}", "config.json");
        try
        {
            var storage = new FileConfigStorage(tempFile);
            var store = new ConfigSyncStore(storage);
            var stateController = new WidgetStateController();
            var protocol = new BridgeProtocol(store, stateController);

            ConfigSnapshot? receivedSnapshot = null;
            store.ConfigChanged += s => receivedSnapshot = s;

            var applyMessage = JsonSerializer.Serialize(new
            {
                type = "config:apply",
                config = new { channel = "  #@ExampleChannel ", fontSize = 30, lang = "en" },
            });

            var result = protocol.HandleIncomingMessage(applyMessage);

            Assert.True(result.Handled);
            Assert.Null(result.ErrorCode);
            Assert.NotNull(result.BroadcastJson);
            Assert.NotNull(receivedSnapshot);
            Assert.Equal("examplechannel", receivedSnapshot!.Config["channel"]);
            Assert.Equal(30d, receivedSnapshot.Config["fontSize"]);

            using var doc = JsonDocument.Parse(result.BroadcastJson!);
            Assert.Equal("config:updated", doc.RootElement.GetProperty("type").GetString());
            var hash = doc.RootElement.GetProperty("hash").GetString();
            Assert.StartsWith("#c=1.", hash);
            Assert.Equal($"ms-appx-web:///WebAssets/overlay.html{hash}", doc.RootElement.GetProperty("overlayUri").GetString());

            // Reloading from disk restores the exact persisted settings
            var reloadedStore = new ConfigSyncStore(new FileConfigStorage(tempFile));
            Assert.Equal("examplechannel", reloadedStore.GetSnapshot().Config["channel"]);
            Assert.Equal(hash, reloadedStore.GetSnapshot().Hash);
        }
        finally
        {
            var dir = Path.GetDirectoryName(tempFile);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void StateQueryAndHashApply_ReturnConsistentStateAndConfigEnvelopes()
    {
        var store = new ConfigSyncStore();
        var stateController = new WidgetStateController();
        stateController.UpdateHostState(new WidgetHostSnapshot(true, true, false, 0.65, false));
        var protocol = new BridgeProtocol(store, stateController);

        var openSettingsCount = 0;
        stateController.OpenSettingsRequested += () => openSettingsCount++;

        var custom = ConfigCodec.GetDefaultConfig();
        custom["channel"] = "overlaytest";
        var hash = ConfigCodec.EncodeConfigHash(custom);

        var hashApply = protocol.HandleIncomingMessage(JsonSerializer.Serialize(new { type = "config:apply", input = hash }));
        Assert.True(hashApply.Handled);

        var query = protocol.HandleIncomingMessage("{\"type\":\"state:query\"}");
        Assert.True(query.Handled);
        using var queryDoc = JsonDocument.Parse(query.ResponseJson!);
        Assert.Equal("state:update", queryDoc.RootElement.GetProperty("type").GetString());
        Assert.Equal(hash, queryDoc.RootElement.GetProperty("hash").GetString());
        Assert.True(queryDoc.RootElement.GetProperty("state").GetProperty("isTransparentClickThrough").GetBoolean());
        Assert.Equal(0.65, queryDoc.RootElement.GetProperty("state").GetProperty("chatOpacity").GetDouble());

        var openResult = protocol.HandleIncomingMessage("{\"type\":\"settings:open\"}");
        Assert.True(openResult.Handled);
        Assert.Equal(1, openSettingsCount);

        var directApply = store.ApplyConfig(new Dictionary<string, object?> { ["channel"] = "direct" });
        Assert.Equal("direct", directApply.Config["channel"]);
    }
}
