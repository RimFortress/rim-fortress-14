using Content.Shared._RF.NPC.Engagement;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._RF.NPC.Engagement;

/// <summary>
/// Keeps what the server says about the situations (engagements) for the debug overlay (see <see cref="EngagementsDebugOverlay"/>),
/// and turns the overlay on and off. The client sees only the entities that are near it, so the server tells who is in what.
/// </summary>
public sealed partial class EngagementDebugSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = null!;
    [Dependency] private IGameTiming _timing = null!;

    /// <summary>
    /// The last snapshot, null before the first one has come.
    /// </summary>
    public EngagementsDebugMessage? Snapshot { get; private set; }

    public TimeSpan SnapshotTime { get; private set; }

    /// <summary>
    /// The kinds that are shown: the parts of their names, separated by commas, empty for all of them (<c>RaidSquad,Raid</c>).
    /// </summary>
    public string Filter { get; set; } = string.Empty;

    public bool Enabled
    {
        get => _overlay.HasOverlay<EngagementsDebugOverlay>();
        set
        {
            if (value == Enabled)
                return;

            if (value)
                _overlay.AddOverlay(new EngagementsDebugOverlay());
            else
            {
                _overlay.RemoveOverlay<EngagementsDebugOverlay>();
                Snapshot = null;
            }

            RaiseNetworkEvent(new EngagementsDebugRequest(value));
        }
    }

    public bool Matches(EngagementDebugInfo engagement)
    {
        if (string.IsNullOrWhiteSpace(Filter))
            return true;

        foreach (var part in Filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (engagement.Kind.Contains(part, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    [SubscribeNetworkEvent]
    private void OnSnapshot(EngagementsDebugMessage msg)
    {
        if (!Enabled)
            return;

        Snapshot = msg;
        SnapshotTime = _timing.CurTime;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<EngagementsDebugOverlay>();
    }
}
