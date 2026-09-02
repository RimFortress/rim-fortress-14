using Content.Server.Hands.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Forces the entity to drop the object in its hands at the specified coordinates
/// </summary>
public sealed partial class ThrowAt : BaseGoapAction<ThrowAt>
{
    /// <summary>
    /// The key with the coordinates for the throw
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> TargetCoordinatesKey = "TargetCoordinates";
}

public sealed partial class ThrowAtGoapActionSystem : GoapActionSystem<ThrowAt>
{
    [Dependency] private HandsSystem _hands = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, ThrowAt action)
        => TryGet(ent, action.TargetCoordinatesKey, out var coords)
           && _hands.ThrowHeldItem(ent, coords);
}
