using CustomTwitchChat.GameBar.Core.State;
using Xunit;

namespace CustomTwitchChat.GameBar.Tests.State;

public sealed class WidgetStateControllerTests
{
    [Fact]
    public void InitialState_IsUnpinnedInteractiveWithFullOpacity()
    {
        var controller = new WidgetStateController();

        Assert.False(controller.CurrentState.IsPinned);
        Assert.False(controller.CurrentState.IsTransparentClickThrough);
        Assert.False(controller.CurrentState.HideChrome);
        Assert.Equal(1.0, controller.CurrentState.EffectiveOpacity);
        Assert.Equal(1.0, controller.CurrentState.ChatOpacity);
        Assert.Equal(WidgetStateController.UnpinnedClass, controller.CurrentState.CssModeClass);
        Assert.False(controller.CurrentSnapshot.IsFavorited);
    }

    [Fact]
    public void UpdateHostState_EnablesTransparentClickThroughWhenPinnedAndGameBarClosedOrClickThroughActive()
    {
        var controller = new WidgetStateController();
        var observed = new List<OverlayVisualState>();
        controller.StateChanged += state => observed.Add(state);

        // Pinned while Game Bar menu is still open and click-through off -> interactive pinned
        var interactive = controller.UpdateHostState(new WidgetHostSnapshot(
            IsPinned: true,
            IsClickThroughEnabled: false,
            IsGameBarOpen: true,
            RequestedOpacity: 0.75,
            IsFavorited: true));

        Assert.False(interactive.IsTransparentClickThrough);
        Assert.False(interactive.HideChrome);
        Assert.Equal(0.75, interactive.EffectiveOpacity);
        Assert.Equal(0.75, interactive.ChatOpacity);
        Assert.Equal(WidgetStateController.PinnedInteractiveClass, interactive.CssModeClass);

        // Pinned and Game Bar closed -> transparent click-through over fullscreen game
        var closedOverlay = controller.UpdateHostState(new WidgetHostSnapshot(
            IsPinned: true,
            IsClickThroughEnabled: false,
            IsGameBarOpen: false,
            RequestedOpacity: 0.8,
            IsFavorited: true));

        Assert.True(closedOverlay.IsTransparentClickThrough);
        Assert.True(closedOverlay.HideChrome);
        Assert.Equal(0.0, closedOverlay.EffectiveOpacity);
        Assert.Equal(0.8, closedOverlay.ChatOpacity);
        Assert.Equal(WidgetStateController.PinnedClickThroughClass, closedOverlay.CssModeClass);

        // Identical update should not fire duplicate StateChanged event
        controller.UpdateHostState(new WidgetHostSnapshot(
            IsPinned: true,
            IsClickThroughEnabled: false,
            IsGameBarOpen: false,
            RequestedOpacity: 0.8,
            IsFavorited: true));

        // Pinned with explicit ClickThroughEnabled while Game Bar is open -> transparent click-through
        var explicitClickThrough = controller.UpdateHostState(new WidgetHostSnapshot(
            IsPinned: true,
            IsClickThroughEnabled: true,
            IsGameBarOpen: true,
            RequestedOpacity: 50,
            IsFavorited: false));

        Assert.True(explicitClickThrough.IsTransparentClickThrough);
        Assert.Equal(0.5, explicitClickThrough.ChatOpacity);
        Assert.Equal(3, observed.Count);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(double.NaN, 0.0)]
    [InlineData(double.PositiveInfinity, 0.0)]
    [InlineData(0.35, 0.35)]
    [InlineData(75.0, 0.75)]
    [InlineData(150.0, 1.0)]
    public void NormalizeOpacity_ClampsAndScalesPercentages(double input, double expected)
    {
        Assert.Equal(expected, WidgetStateController.NormalizeOpacity(input), precision: 4);
    }

    [Fact]
    public void RequestOpenSettings_RaisesEvent()
    {
        var controller = new WidgetStateController();
        var called = 0;
        controller.OpenSettingsRequested += () => called++;

        controller.RequestOpenSettings();
        Assert.Equal(1, called);
    }
}
