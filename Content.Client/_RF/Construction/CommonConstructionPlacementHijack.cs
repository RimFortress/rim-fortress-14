using System.Linq;
using Content.Client.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Placement;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

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

        if (prototype is null || !system.TryGetRecipePrototype(prototype.ID, out var targetProtoId))
            return;

        if (!IoCManager.Resolve<IPrototypeManager>().TryIndex(targetProtoId, out var proto))
            return;

        manager.CurrentTextures = SpriteComponent.GetPrototypeTextures(proto, IoCManager.Resolve<IResourceCache>()).ToList();
    }

    public override bool HijackDeletion(EntityUid entity)
    {
        system.ClearGhost(entity);
        return true;
    }
}
