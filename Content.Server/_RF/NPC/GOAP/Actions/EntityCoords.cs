using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.GOAP.Actions;

/// <summary>
/// Gets the coordinates of the entity
/// </summary>
public sealed partial class EntityCoords : BaseGoapAction<EntityCoords>
{
    [DataField]
    public StateKey<EntityUid> TargetKey = "Target";

    [DataField]
    public StateKey<EntityCoordinates> CoordinatesKey = "TargetCoordinates";
}

public sealed class EntityCoordsSystem : GoapActionSystem<EntityCoords>
{
    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, EntityCoords action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, EntityCoords action)
    {
        if (!TryGet(ent, action.TargetKey, out var uid))
            return false;

        ent.Comp.State.SetValue(action.CoordinatesKey, Transform(uid).Coordinates);
        return true;
    }
}
