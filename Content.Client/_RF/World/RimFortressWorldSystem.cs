using System.Linq;
using Content.Shared._RF.World;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client._RF.World;

public sealed class RimFortressWorldSystem : SharedRimFortressWorldSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public Dictionary<EntityUid, List<EntityCoordinates>> Settlemets { get; private set; } = new();

    public bool EnableOverlay
    {
        get => _enableOverlay;
        set
        {
            _enableOverlay = value;

            if (_enableOverlay)
                _overlay.AddOverlay(new WorldOverlay());
            else
                _overlay.RemoveOverlay<WorldOverlay>();

            RaiseNetworkEvent(new WorldDebugInfoRequest());
        }
    }

    private bool _enableOverlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SettlementCoordinatesMessage>(OnSettlementCoordinates);
    }

    private void OnSettlementCoordinates(SettlementCoordinatesMessage msg)
    {
        Settlemets = msg.Coords
            .Select(x =>
                (GetEntity(x.Key), x.Value.Select(GetCoordinates).ToList()))
            .ToDictionary();
    }
}
