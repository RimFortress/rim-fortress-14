using Content.Server.NPC;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Queries.Filters;

/// <summary>
/// Filters the entities for the presence of tags
/// </summary>
public sealed partial class TagFilter : RfUtilityQueryFilter
{
    private TagSystem _tag = default!;

    /// <summary>
    /// Tags to be filtered out
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<TagPrototype>> Tags = default!;

    /// <summary>
    /// Do you need to check for all tags or just one tag
    /// </summary>
    [DataField]
    public bool RequireAll = true;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _tag = entManager.System<TagSystem>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return RequireAll && _tag.HasAllTags(uid, Tags)
               || !RequireAll && _tag.HasAnyTag(uid, Tags);
    }
}
