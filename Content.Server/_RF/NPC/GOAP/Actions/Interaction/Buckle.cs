using Content.Server.Buckle.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// The agent buckles the target entity to another one.
/// </summary>
public sealed partial class Buckle : BaseGoapAction<Buckle>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = GoapState.Owner;

    /// <summary>
    /// The entity to which the target needs to be buckled.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> BuckleTo;
}

public sealed partial class BuckleGoapActionSystem : GoapActionSystem<Buckle>
{
    [Dependency] private BuckleSystem _buckle = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Buckle action)
        => TryGet(ent, action.TargetKey, out var target)
           && TryGet(ent, action.BuckleTo, out var buckleTo)
           && _buckle.TryBuckle(target, ent, buckleTo);
}
