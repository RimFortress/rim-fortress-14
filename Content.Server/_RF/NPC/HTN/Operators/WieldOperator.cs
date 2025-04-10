using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Hands.Components;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Toggle item wield
/// </summary>
public sealed partial class WieldOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private SharedWieldableSystem _wield = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _wield = sysManager.GetEntitySystem<SharedWieldableSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.Owner, out var owner, _entManager)
            || !blackboard.TryGetValue<Hand>(NPCBlackboard.ActiveHand, out var hand, _entManager)
            || hand.HeldEntity is not { } entity
            || !_entManager.TryGetComponent(entity, out WieldableComponent? wield))
            return HTNOperatorStatus.Failed;

        if (wield.Wielded)
            _wield.TryUnwield(entity, wield, owner);
        else
            _wield.TryWield(entity, wield, owner);

        return HTNOperatorStatus.Finished;
    }
}
