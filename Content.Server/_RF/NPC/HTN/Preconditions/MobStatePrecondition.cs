using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks current entity MobState
/// </summary>
public sealed partial class MobStatePrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string TargetKey = default!;

    [DataField(required: true)]
    public MobState State = MobState.Alive;

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager)
            && _entManager.TryGetComponent(target, out MobStateComponent? state)
            && state.CurrentState == State)
            return !Invert;

        return Invert;
    }
}
