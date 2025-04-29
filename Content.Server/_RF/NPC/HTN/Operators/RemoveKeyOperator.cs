using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Deletes a key from blackboard
/// </summary>
public sealed partial class RemoveKeyOperator : HTNOperator
{
    [DataField(required: true)]
    public string Key = default!;

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.ContainsKey(Key))
            return HTNOperatorStatus.Failed;

        blackboard.Remove(Key);
        return HTNOperatorStatus.Finished;
    }
}

/// <summary>
/// Deletes a keys from blackboard
/// </summary>
public sealed partial class RemoveKeysOperator : HTNOperator
{
    [DataField(required: true)]
    public List<string> Keys = default!;

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        foreach (var key in Keys)
        {
            if (!blackboard.ContainsKey(key))
                return HTNOperatorStatus.Failed;
        }

        foreach (var key in Keys)
        {
            blackboard.Remove(key);
        }

        return HTNOperatorStatus.Finished;
    }
}
