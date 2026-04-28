using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.Components;

namespace Content.Server._RF.NPC.HTN.Operators;

public sealed partial class MarkOwnerOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private OwnershipSystem _ownership;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _ownership = sysManager.GetEntitySystem<OwnershipSystem>();
    }

    [DataField(required: true)]
    public string TargetKey;

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entityManager.TryGetComponent(owner, out ControllableNpcComponent? control)
            || !blackboard.TryGetValue(TargetKey, out EntityUid? uid, _entityManager))
            return;

        _ownership.AddOwners(uid.Value, control.CanControl);
    }
}

