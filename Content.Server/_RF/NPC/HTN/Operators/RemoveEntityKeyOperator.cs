using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Deletes a key of type EntityUid from blackboard
/// </summary>
public sealed partial class RemoveEntityKeyOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string Key = default!;

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue(Key, out EntityUid _, _entManager))
            return HTNOperatorStatus.Failed;

        blackboard.Remove<EntityUid>(Key);
        return HTNOperatorStatus.Finished;
    }
}
