using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// The agent begins to pull the target entity.
/// </summary>
public sealed partial class Pull : BaseGoapAction<Pull>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed class PullGoapActionSystem : GoapActionSystem<Pull>
{
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Pull action)
    {
        if (!TryGet(ent, action.TargetKey, out var target))
            return true;

        if (!_transform.InRange(ent.Owner, target, Get(ent, GoapState.InteractRange)))
        {
            CreateDump("target not in interaction range");
            return false;
        }

        return _pulling.TryStartPull(ent, target);
    }

    protected override void ActionPlanShutdown(Entity<GoapComponent> ent, Pull action, GoapPlanFinishReason reason)
    {
        if (TryGet(ent, action.TargetKey, out var target)
            && TryComp(target, out PullableComponent? pullable))
            _pulling.TryStopPull(target, pullable , ent.Owner);
    }
}
