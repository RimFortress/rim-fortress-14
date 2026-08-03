using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.Buckle.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Unbuckles the target entity.
/// </summary>
public sealed partial class Unbuckle : BaseGoapAction<Unbuckle>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = GoapState.Owner;
}

public sealed class UnbuckleSystem : GoapActionSystem<Unbuckle>
{
    [Dependency] private readonly BuckleSystem _buckle = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, Unbuckle action) => 0.5f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Unbuckle action)
        => TryGetValue(ent.Comp.State, action, action.TargetKey, out var target)
           && _buckle.TryUnbuckle(target, ent, false);
}
