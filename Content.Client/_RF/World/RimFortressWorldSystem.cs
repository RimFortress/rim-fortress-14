using System.Linq;
using Content.Shared._RF.World;
using Content.Shared._RF.World.Components;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client._RF.World;

public sealed partial class RimFortressWorldSystem : SharedRimFortressWorldSystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public Dictionary<EntityUid, List<EntityCoordinates>> Settlements { get; private set; } = new();

    public event Action<Entity<RimFortressPlayerComponent>>? OnPlayerUpdate;

    public bool EnableOverlay
    {
        get;
        set
        {
            field = value;

            if (field)
                _overlay.AddOverlay(new WorldOverlay());
            else
                _overlay.RemoveOverlay<WorldOverlay>();

            RaiseNetworkEvent(new WorldDebugInfoRequest());
        }
    }

    [SubscribeLocalEvent]
    private void OnPlayerHandleState(Entity<RimFortressPlayerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        OnPlayerUpdate?.Invoke(ent);
    }

    [SubscribeNetworkEvent]
    private void OnSettlementCoordinates(SettlementCoordinatesMessage msg)
    {
        Settlements = msg.Coords
            .Select(x =>
                (GetEntity(x.Key), x.Value.Select(GetCoordinates).ToList()))
            .ToDictionary();
    }
}
