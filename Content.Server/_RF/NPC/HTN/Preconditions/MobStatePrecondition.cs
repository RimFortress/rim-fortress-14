using Content.Server.NPC;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks current entity MobState
/// </summary>
public sealed partial class MobStatePrecondition : InvertiblePrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string TargetKey = default!;

    [DataField(required: true)]
    public MobState State = MobState.Alive;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager)
               && _entManager.TryGetComponent(target, out MobStateComponent? state)
               && state.CurrentState == State;
    }
}
