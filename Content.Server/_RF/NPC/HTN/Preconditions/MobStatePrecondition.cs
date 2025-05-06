using Content.Server.NPC;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks current entity MobState
/// </summary>
public sealed partial class MobStatePrecondition : InvertiblePrecondition
{
    [DataField(required: true)]
    public string TargetKey = default!;

    [DataField(required: true)]
    public MobState State = MobState.Alive;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue<EntityUid>(TargetKey, out var target, EntityManager)
               && EntityManager.TryGetComponent(target, out MobStateComponent? state)
               && state.CurrentState == State;
    }
}
