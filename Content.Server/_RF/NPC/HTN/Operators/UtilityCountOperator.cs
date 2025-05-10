using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Queries;
using Content.Server.NPC.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Checks the number of entities in the query and fails if the conditions do not match.
/// Yes, it should be a condition, but this is more optimized because of the async operation
/// </summary>
public sealed partial class UtilityCountOperator : HTNOperator
{
    private NPCUtilitySystem _utility;

    /// <summary>
    /// Utility query, the number of entities in which to check
    /// </summary>
    [DataField(required: true)]
    public ProtoId<UtilityQueryPrototype> Proto;

    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _utility = sysManager.GetEntitySystem<NPCUtilitySystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var result = _utility.GetEntities(blackboard, Proto);
        var valid = MoreThan != null && result.Entities.Count > MoreThan
                    || LessThan != null && result.Entities.Count < LessThan;

        return (valid, null);
    }
}
