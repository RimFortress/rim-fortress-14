using Content.Server.Hands.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.HTN.Operators.Interaction;

/// <summary>
/// Forces the entity to drop the object in its hands at the specified coordinates
/// </summary>
public sealed partial class ThrowAtOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private HandsSystem _hands;

    /// <summary>
    /// The key with the coordinates for the throw
    /// </summary>
    [DataField]
    public string TargetCoordinates = "TargetCoordinates";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _hands = sysManager.GetEntitySystem<HandsSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<EntityCoordinates>(TargetCoordinates, out var coord, _entity))
            return HTNOperatorStatus.Failed;

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        return _hands.ThrowHeldItem(owner, coord)
           ? HTNOperatorStatus.Finished
           : HTNOperatorStatus.Failed;
    }
}
