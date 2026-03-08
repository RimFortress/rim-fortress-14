using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators;

public abstract partial class IterateOperator<T> : HTNOperator where T: notnull
{
    [Dependency] private readonly IEntityManager _entity = default!;

    /// <summary>
    /// Key that stores the list to be iterated
    /// </summary>
    [DataField(required: true)]
    public string TargetKey;

    /// <summary>
    /// Key in which the list item will be saved
    /// </summary>
    [DataField(required: true)]
    public string ResultKey;

    /// <summary>
    /// Will the operator iterate the list in a circle,
    /// or will fail in planning when reaching the end of the list
    /// </summary>
    [DataField]
    public bool Repeat;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<List<T>>(TargetKey, out var list, _entity) || list.Count == 0)
            return (false, null);

        if (!blackboard.TryGetValue<T>(ResultKey, out var item, _entity))
            return (true, new() { { ResultKey, list[0] } });

        var index = list.IndexOf(item);

        if (index == -1)
            return (true, new() { { ResultKey, list[0] } });

        if (index + 1 < list.Count)
            return (true, new() { { ResultKey, list[index + 1] } });

        if (Repeat)
            return (true, new() { { ResultKey, list[0] } });

        return (false, null);
    }
}

/// <summary>
/// Iterates the list of entities and saves the result to a key
/// </summary>
public sealed partial class IterateEntitiesOperator : IterateOperator<EntityUid>;
