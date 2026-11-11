using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.GOAP.Actions.Movement;

/// <summary>
/// Rotates the agent to the specified place.
/// </summary>
public sealed partial class RotateToTarget : BaseGoapAction<RotateToTarget>
{
    /// <summary>
    /// A key that stores the coordinates for rotation.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> TargetKey = "TargetCoordinates";
}

public sealed partial class RotateToTargetSystem : GoapActionSystem<RotateToTarget>
{
    [Dependency] private RotateToFaceSystem _rotate = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, RotateToTarget action) => 0.5f;

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, RotateToTarget action)
        => TryGet(ent, action.TargetKey, out var coords)
           && _rotate.TryFaceCoordinates(ent, coords.Position)
            ? GoapActionResult.Finished
            : GoapActionResult.Failed;
}
