using System.Linq;
using Content.Client.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Client.Placement;
using Robust.Client.Utility;
using Robust.Shared.Map;

namespace Content.Client._RF.Construction;

/// <summary>
/// Server-side placement hijack, unlike the <see cref="ConstructionPlacementHijack"/>
/// </summary>
public sealed class CommonConstructionPlacementHijack(
    CommonConstructionSystem system,
    ConstructionPrototype? prototype) : PlacementHijack
{
    public override bool CanRotate { get; } = prototype?.CanRotate ?? false;

    public override bool HijackPlacementRequest(EntityCoordinates coordinates)
    {
        if (prototype == null)
            return false;

        system.RequestSpawnGhost(prototype, coordinates, Manager.Direction);
        return true;
    }

    public override void StartHijack(PlacementManager manager)
    {
        base.StartHijack(manager);
        manager.CurrentTextures = prototype?.Layers.Select(sprite => sprite.DirFrame0()).ToList();
    }

    public override bool HijackDeletion(EntityUid entity)
    {
        system.ClearGhost(entity);
        return true;
    }
}
