using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Deletes a key of a given type from blackboard
/// </summary>
public abstract partial class RemoveKeyOperator<T> : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string Key = default!;

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<T>(Key, out _, _entManager))
            return HTNOperatorStatus.Failed;

        blackboard.Remove<T>(Key);
        return HTNOperatorStatus.Finished;
    }
}

public sealed partial class RemoveEntityKeyOperator : RemoveKeyOperator<EntityUid>
{

}

public sealed partial class RemoveEntityCoordinatesKeyOperator : RemoveKeyOperator<EntityCoordinates>
{

}

public sealed partial class RemoveStringListKeyOperator : RemoveKeyOperator<List<string>>
{

}

public sealed partial class RemoveBoolListKeyOperator : RemoveKeyOperator<List<bool>>
{

}

public sealed partial class RemoveIntListKeyOperator : RemoveKeyOperator<List<int>>
{

}

public sealed partial class RemoveFloatListKeyOperator : RemoveKeyOperator<List<float>>
{

}
