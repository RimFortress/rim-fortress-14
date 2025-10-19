using Content.Server.Hands.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;

namespace Content.Server._RF.NPC.HTN.Operators.Interaction;

/// <summary>
/// Toggle item wield
/// </summary>
public sealed partial class WieldOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private SharedWieldableSystem _wield = default!;
    private HandsSystem _hands = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _wield = sysManager.GetEntitySystem<SharedWieldableSystem>();
        _hands = sysManager.GetEntitySystem<HandsSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.Owner, out var owner, _entManager)
            || !_hands.TryGetActiveItem(owner, out var entity)
            || !_entManager.TryGetComponent(entity, out WieldableComponent? wield))
            return HTNOperatorStatus.Failed;

        if (wield.Wielded)
            _wield.TryUnwield(entity.Value, wield, owner);
        else
            _wield.TryWield(entity.Value, wield, owner);

        return HTNOperatorStatus.Finished;
    }
}
