namespace CustomTwitchChat.GameBar.Core.State;

/// <summary>
/// Snapshot of native Xbox Game Bar widget properties reported by the UWP host.
/// </summary>
public sealed record WidgetHostSnapshot(
    bool IsPinned,
    bool IsClickThroughEnabled,
    bool IsGameBarOpen,
    double RequestedOpacity,
    bool IsFavorited);

/// <summary>
/// Computed visual and interaction state passed to the overlay WebView.
/// </summary>
public sealed record OverlayVisualState(
    bool IsPinned,
    bool IsClickThroughEnabled,
    bool IsGameBarOpen,
    bool IsTransparentClickThrough,
    bool HideChrome,
    double EffectiveOpacity,
    double ChatOpacity,
    string CssModeClass);

/// <summary>
/// Pure state machine translating Game Bar host transitions into overlay visual state.
/// </summary>
public sealed class WidgetStateController
{
    public const string PinnedClickThroughClass = "gb-pinned-clickthrough";
    public const string PinnedInteractiveClass = "gb-pinned-interactive";
    public const string UnpinnedClass = "gb-unpinned";

    public WidgetStateController()
    {
        CurrentSnapshot = new WidgetHostSnapshot(
            IsPinned: false,
            IsClickThroughEnabled: false,
            IsGameBarOpen: true,
            RequestedOpacity: 1.0,
            IsFavorited: false);
        CurrentState = ComputeState(CurrentSnapshot);
    }

    public WidgetHostSnapshot CurrentSnapshot { get; private set; }

    public OverlayVisualState CurrentState { get; private set; }

    public event Action<OverlayVisualState>? StateChanged;

    public event Action? OpenSettingsRequested;

    public OverlayVisualState UpdateHostState(WidgetHostSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        CurrentSnapshot = snapshot;
        var next = ComputeState(snapshot);
        var changed = next != CurrentState;
        CurrentState = next;
        if (changed)
        {
            StateChanged?.Invoke(next);
        }
        return next;
    }

    public void RequestOpenSettings() => OpenSettingsRequested?.Invoke();

    public static double NormalizeOpacity(double requestedOpacity)
    {
        if (!double.IsFinite(requestedOpacity) || requestedOpacity < 0.0) return 0.0;
        if (requestedOpacity > 1.0 && requestedOpacity <= 100.0) return requestedOpacity / 100.0;
        if (requestedOpacity > 100.0) return 1.0;
        return requestedOpacity;
    }

    public static OverlayVisualState ComputeState(WidgetHostSnapshot snapshot)
    {
        var chatOpacity = NormalizeOpacity(snapshot.RequestedOpacity);
        var transparentClickThrough = snapshot.IsPinned && (snapshot.IsClickThroughEnabled || !snapshot.IsGameBarOpen);
        var effectiveOpacity = transparentClickThrough ? 0.0 : chatOpacity;
        var cssModeClass = transparentClickThrough
            ? PinnedClickThroughClass
            : (snapshot.IsPinned ? PinnedInteractiveClass : UnpinnedClass);

        return new OverlayVisualState(
            IsPinned: snapshot.IsPinned,
            IsClickThroughEnabled: snapshot.IsClickThroughEnabled,
            IsGameBarOpen: snapshot.IsGameBarOpen,
            IsTransparentClickThrough: transparentClickThrough,
            HideChrome: transparentClickThrough,
            EffectiveOpacity: effectiveOpacity,
            ChatOpacity: chatOpacity,
            CssModeClass: cssModeClass);
    }
}
