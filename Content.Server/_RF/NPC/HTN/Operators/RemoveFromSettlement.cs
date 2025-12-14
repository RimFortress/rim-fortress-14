using System.Linq;
using Content.Server._RF.NPC.Components;
using Content.Server._RF.World;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Removes the target entity from the list of any settlements.
/// Use with caution
/// </summary>
/// <remarks>
/// An entity cannot be removed from the player's control if it is the only one
/// </remarks>
public sealed partial class RemoveFromSettlement: HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private RimFortressWorldSystem _world = default!;

    /// <summary>
    /// Target entity
    /// </summary>
    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _world = sysManager.GetEntitySystem<RimFortressWorldSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var uid, _entity)
            || !_entity.TryGetComponent(uid, out ControllableNpcComponent? controllable))
            return HTNOperatorStatus.Failed;

        foreach (var player in controllable.CanControl.ToList())
        {
            if (!_world.RemovePop(player, uid))
                return HTNOperatorStatus.Failed;
        }

        return HTNOperatorStatus.Finished;
    }
}
