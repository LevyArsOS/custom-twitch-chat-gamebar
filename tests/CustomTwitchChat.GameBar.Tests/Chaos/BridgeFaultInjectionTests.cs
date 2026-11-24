using CustomTwitchChat.GameBar.Core.Bridge;
using CustomTwitchChat.GameBar.Core.State;
using Xunit;

namespace CustomTwitchChat.GameBar.Tests.Chaos;

public sealed class BridgeFaultInjectionTests
{
    private sealed class FailingStorage(bool throwOnLoad, bool throwOnSave, string? initialRaw = null) : IConfigStorage
    {
        public string? LoadRaw() => throwOnLoad ? throw new IOException("Disk read fault") : initialRaw;
        public void SaveRaw(string json)
        {
            if (throwOnSave) throw new IOException("Disk write fault");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{truncated")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"noType\": true}")]
    public void BridgeProtocol_HandlesMalformedEnvelopesWithoutThrowing(string? payload)
    {
        var protocol = new BridgeProtocol(new ConfigSyncStore(), new WidgetStateController());
        var result = protocol.HandleIncomingMessage(payload);

        Assert.False(result.Handled);
        Assert.Equal("invalid_message", result.ErrorCode);
        Assert.Null(result.ResponseJson);
    }

    [Theory]
    [InlineData("{\"type\":\"config:apply\"}")]
    [InlineData("{\"type\":\"config:apply\",\"config\":123}")]
    [InlineData("{\"type\":\"config:apply\",\"input\":\"   \"}")]
    [InlineData("{\"type\":\"config:apply\",\"input\":\"not valid json\"}")]
    [InlineData("{\"type\":\"config:apply\",\"input\":\"[1,2]\"}")]
    public void BridgeProtocol_RejectsCorruptConfigPayloadsWhilePreservingPreviousState(string payload)
    {
        var store = new ConfigSyncStore();
        store.ApplyConfig(new Dictionary<string, object?> { ["channel"] = "safe" });
        var protocol = new BridgeProtocol(store, new WidgetStateController());

        var result = protocol.HandleIncomingMessage(payload);

        Assert.False(result.Handled);
        Assert.Equal("invalid_config", result.ErrorCode);
        Assert.Equal("safe", store.GetSnapshot().Config["channel"]);
    }

    [Fact]
    public void BridgeProtocol_ReturnsUnsupportedTypeForUnknownMessages()
    {
        var protocol = new BridgeProtocol(new ConfigSyncStore(), new WidgetStateController());
        var result = protocol.HandleIncomingMessage("{\"type\":\"unknown:op\"}");

        Assert.False(result.Handled);
        Assert.Equal("unsupported_type", result.ErrorCode);
    }

    [Fact]
    public void ConfigSyncStore_SurvivesStorageReadWriteFaultsAndCorruptSavedJson()
    {
        var readFailStore = new ConfigSyncStore(new FailingStorage(throwOnLoad: true, throwOnSave: false));
        Assert.Equal("Disk read fault", readFailStore.LastPersistenceError);
        Assert.Equal("", readFailStore.GetSnapshot().Config["channel"]);

        var writeFailStore = new ConfigSyncStore(new FailingStorage(throwOnLoad: false, throwOnSave: true, initialRaw: "{corrupt"));
        Assert.Null(writeFailStore.LastPersistenceError);
        var updated = writeFailStore.ApplyConfig(new Dictionary<string, object?> { ["channel"] = "resilient" });
        Assert.Equal("Disk write fault", writeFailStore.LastPersistenceError);
        Assert.Equal("resilient", updated.Config["channel"]);

        Assert.Throws<ArgumentException>(() => new FileConfigStorage(" "));
    }

    [Fact]
    public async Task ConfigSyncStore_HandlesConcurrentUpdatesSafely()
    {
        var store = new ConfigSyncStore();
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            store.ApplyConfig(new Dictionary<string, object?>
            {
                ["channel"] = $"chan{i}",
                ["fontSize"] = 16 + (i % 10),
            });
            return store.GetSnapshot();
        }));

        var snapshots = await Task.WhenAll(tasks);
        Assert.Equal(20, snapshots.Length);
        Assert.All(snapshots, s => Assert.StartsWith("#c=1.", s.Hash));
        Assert.StartsWith("chan", (string)store.GetSnapshot().Config["channel"]);
    }
}
