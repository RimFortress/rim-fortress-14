using System.Linq;
using Content.Server.NPC;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Queries.Filters;

/// <summary>
/// Filters tools with a certain quality
/// </summary>
public sealed partial class ToolQualityFilter : RfUtilityQueryFilter
{
    private EntityQuery<ToolComponent> _toolQuery;

    /// <summary>
    /// Quality to be filtered out
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ToolQualityPrototype> Quality;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _toolQuery = entManager.GetEntityQuery<ToolComponent>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _toolQuery.TryComp(uid, out var comp) && comp.Qualities.ToList().Contains(Quality);
    }
}
