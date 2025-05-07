using System.Linq;
using Content.Server.NPC;
using Content.Shared.Tools.Components;

namespace Content.Server._RF.NPC.Queries.Filters;

/// <summary>
/// Filters tools with a certain quality
/// </summary>
public sealed partial class ToolQualityKeyFilter : RfUtilityQueryFilter
{
    private EntityQuery<ToolComponent> _toolQuery;

    /// <summary>
    /// Quality to be filtered out
    /// </summary>
    [DataField(required: true)]
    public string QualityKey = default!;

    private string? _quality;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _toolQuery = entManager.GetEntityQuery<ToolComponent>();
    }

    public override bool Startup(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(QualityKey, out _quality, EntityManager);
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _toolQuery.TryComp(uid, out var comp)
               && comp.Qualities.ToList().Contains(_quality!);
    }
}
