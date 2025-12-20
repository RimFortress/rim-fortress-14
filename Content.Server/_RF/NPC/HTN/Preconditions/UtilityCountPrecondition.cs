using Content.Server.NPC;
using Content.Server.NPC.Queries;
using Content.Server.NPC.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the number of entities in utilityQuery
/// </summary>
public sealed partial class UtilityCountPrecondition : InvertiblePrecondition
{
    private NPCUtilitySystem _utility;

    [DataField(required: true)]
    public ProtoId<UtilityQueryPrototype> Query;

    [DataField]
    public int? LessThan;

    [DataField]
    public int MoreThan;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _utility = sysManager.GetEntitySystem<NPCUtilitySystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        var result = _utility.GetEntities(blackboard, Query);
        return result.Entities.Count > MoreThan || result.Entities.Count < LessThan;
    }
}
