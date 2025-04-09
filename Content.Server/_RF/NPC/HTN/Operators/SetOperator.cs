using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Set value for key in blackboard
/// </summary>
public abstract partial class SetOperator<T> : HTNOperator
{
    [DataField(required: true)]
    public string Key = default!;
    
    [DataField(required: true)]
    public T Value = default!;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        return (true, new() { {Key, Value!} });
    }
}

public sealed partial class SetStringOperator: SetOperator<string>
{
    
}
