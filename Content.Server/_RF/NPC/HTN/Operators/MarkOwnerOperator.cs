using Content.Server._RF.NPC.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC.HTN.Operators;

public sealed partial class MarkOwnerOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    [DataField(required: true)]
    public string TargetKey;

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entityManager.TryGetComponent(owner, out ControllableNpcComponent? control)
            || !blackboard.TryGetValue(TargetKey, out EntityUid? uid, _entityManager))
            return;

        var comp = _entityManager.EnsureComponent<OwnedComponent>(uid.Value);
        comp.Owners.AddRange(control.CanControl);
    }
}

