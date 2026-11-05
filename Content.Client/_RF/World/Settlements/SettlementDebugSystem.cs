using System.Linq;
using Content.Shared._RF.World.Settlements;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._RF.World.Settlements;

/// <summary>
/// Keeps what the server says about the settlements for the debug overlay (see <see cref="SettlementsDebugOverlay"/>), and turns the
/// overlay on and off. The client sees only the entities that are near it, so the server tells where everything is and who owns it.
/// </summary>
public sealed partial class SettlementDebugSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private SpriteSystem _sprite = null!;

    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery;

    /// <summary>
    /// How much of the color of the settlement the sprite takes: the sprite is still to be told what it is.
    /// </summary>
    private const float TintStrength = 0.65f;

    private static readonly Color WaitingColor = Color.Orange;
    public static readonly Color EventColor = Color.Red;

    // The sprites that are painted and the colors they had, to give them back.
    private readonly Dictionary<EntityUid, Color> _painted = new();

    /// <summary>
    /// The last snapshot, null before the first one has come.
    /// </summary>
    public SettlementsDebugMessage? Snapshot { get; private set; }

    /// <summary>
    /// When the last snapshot has come.
    /// </summary>
    public TimeSpan SnapshotTime { get; private set; }

    public bool Enabled
    {
        get => _overlay.HasOverlay<SettlementsDebugOverlay>();
        set
        {
            if (value == Enabled)
                return;

            if (value)
                _overlay.AddOverlay(new SettlementsDebugOverlay());
            else
            {
                _overlay.RemoveOverlay<SettlementsDebugOverlay>();
                Snapshot = null;
                Unpaint();
            }

            RaiseNetworkEvent(new SettlementsDebugRequest(value));
        }
    }

    [SubscribeNetworkEvent]
    private void OnSnapshot(SettlementsDebugMessage msg)
    {
        // A snapshot can come after the overlay has been turned off (the server sends it before it gets the request): nothing is
        // painted by it, or it would be painted for good.
        if (!Enabled)
            return;

        Snapshot = msg;
        SnapshotTime = _timing.CurTime;
        Paint(msg);
    }

    /// <summary>
    /// Paints the sprites of what the settlements own in the colors of the settlements, and of what waits for one in orange. The
    /// entities that the client has not got (they are far, out of its view) are not painted; they are not seen either.
    /// </summary>
    private void Paint(SettlementsDebugMessage msg)
    {
        var wanted = new Dictionary<EntityUid, Color>();

        foreach (var settlement in msg.Settlements)
        {
            var color = SettlementsDebugOverlay.SettlementColor(settlement.Uid);

            foreach (var member in settlement.Members)
            {
                if (TryGetEntity(member.Uid, out var uid))
                    wanted[uid.Value] = color;
            }
        }

        foreach (var waiting in msg.Waiting)
        {
            if (TryGetEntity(waiting.Uid, out var uid))
                wanted.TryAdd(uid.Value, WaitingColor);
        }

        // The ones who have come with an event are red, whoever they were owned by before.
        foreach (var ev in msg.Events)
        {
            foreach (var entity in ev.Entities)
            {
                if (TryGetEntity(entity.Uid, out var uid))
                    wanted[uid.Value] = EventColor;
            }
        }

        // What is not owned any more, or is gone, gets its color back.
        foreach (var (uid, original) in _painted.ToArray())
        {
            if (wanted.ContainsKey(uid))
                continue;

            Restore(uid, original);
            _painted.Remove(uid);
        }

        foreach (var (uid, color) in wanted)
        {
            if (TerminatingOrDeleted(uid) || !_spriteQuery.TryComp(uid, out var sprite))
                continue;

            if (!_painted.ContainsKey(uid))
                _painted[uid] = sprite.Color;

            _sprite.SetColor((uid, sprite), Color.InterpolateBetween(_painted[uid], color, TintStrength));
        }
    }

    private void Unpaint()
    {
        foreach (var (uid, original) in _painted)
        {
            Restore(uid, original);
        }

        _painted.Clear();
    }

    private void Restore(EntityUid uid, Color original)
    {
        if (!TerminatingOrDeleted(uid) && _spriteQuery.TryComp(uid, out var sprite))
            _sprite.SetColor((uid, sprite), original);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<SettlementsDebugOverlay>();
        Unpaint();
    }
}
