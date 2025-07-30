using Content.Server.Interaction;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.CombatMode;
using Content.Shared.Timing;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Activates an entity as when E is pressed
/// </summary>
public sealed partial class InteractActivateOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private InteractionSystem _interaction = default!;
    private SharedCombatModeSystem _combatMode = default!;

    /// <summary>
    /// Key that contains the target entity.
    /// </summary>
    [DataField(required: true)]
    public string TargetKey = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _interaction = sysManager.GetEntitySystem<InteractionSystem>();
        _combatMode = sysManager.GetEntitySystem<SharedCombatModeSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (_entManager.TryGetComponent<UseDelayComponent>(owner, out var useDelay)
            && _entManager.System<UseDelaySystem>().IsDelayed((owner, useDelay))
            || !blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return HTNOperatorStatus.Continuing;

        if (_entManager.TryGetComponent<CombatModeComponent>(owner, out var combatMode))
            _combatMode.SetInCombatMode(owner, false, combatMode);

        _interaction.InteractionActivate(owner, target);

        return HTNOperatorStatus.Finished;
    }
}
