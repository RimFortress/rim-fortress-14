using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Shared.Utility;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Pop a float from the list by the given key
/// </summary>
public abstract partial class PopOperator<T> : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    /// <summary>
    /// Key with list
    /// </summary>
    [DataField]
    public string Key = default!;

    /// <summary>
    /// The key by which the popped value will be stored
    /// </summary>
    [DataField]
    public string OutKey = default!;

    /// <summary>
    /// Whether the key with the list should be deleted if it turns out to be empty
    /// </summary>
    [DataField]
    public bool RemoveOnEmpty = true;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<List<T>>(Key, out var list, _entManager) || list.Count == 0)
            return (false, null);

        var value = list.Pop();

        return (true, new()
        {
            { OutKey, value! },
            { Key, list },
        });
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (RemoveOnEmpty && blackboard.TryGetValue<List<T>>(Key, out var list, _entManager) && list.Count == 0)
            blackboard.Remove<List<T>>(Key);

        return HTNOperatorStatus.Finished;
    }
}

public sealed partial class PopFloatOperator : PopOperator<float>
{

}

public sealed partial class PopIntOperator : PopOperator<int>
{

}

public sealed partial class PopStringOperator : PopOperator<string>
{

}

public sealed partial class PopStringListOperator : PopOperator<List<string>>
{

}

public sealed partial class PopBoolOperator : PopOperator<bool>
{

}
