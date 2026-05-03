using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.Hands.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// The agent attempts to take the target entity into its active hand.
/// </summary>
public sealed partial class Pickup : BaseGoapAction<Pickup>
{
    /// <summary>
    /// Target entity key.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = "Target";
}

public sealed class PickupSystem : GoapActionSystem<Pickup>
{
    [Dependency] private readonly HandsSystem _hands = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Pickup action)
        => TryGetValue(ent, action, action.TargetKey, out var target)
           && _hands.TryPickup(ent, target);
}
