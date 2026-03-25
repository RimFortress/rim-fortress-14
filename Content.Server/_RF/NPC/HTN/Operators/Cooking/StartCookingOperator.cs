using Content.Server.Kitchen.Components;
using Content.Server.Kitchen.EntitySystems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators.Cooking;

public sealed partial class StartCookingOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;
    private MicrowaveSystem _microwave;

    /// <summary>
    /// The key to storing the kitchen entity.
    /// </summary>
    [DataField]
    public string TargetKey = "Target";

    [DataField]
    public string? RemainingTimeKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _microwave = sysManager.GetEntitySystem<MicrowaveSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entity)
            || !_entity.TryGetComponent(target, out MicrowaveComponent? comp))
            return HTNOperatorStatus.Failed;

        if (_entity.TryGetComponent(target, out ActiveMicrowaveComponent? active))
            return HTNOperatorStatus.Finished;

        _microwave.Wzhzhzh(target, comp, blackboard.GetOwner());

        if (RemainingTimeKey != null && _entity.TryGetComponent(target, out active))
            blackboard.SetValue(RemainingTimeKey, active.CookTimeRemaining);

        return HTNOperatorStatus.Finished;
    }
}
