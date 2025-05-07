using System.Linq;
using Content.Server.NPC;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Queries.Filters;

/// <summary>
/// Filters the entities for the presence of tags
/// </summary>
public sealed partial class TagKeyFilter : RfUtilityQueryFilter
{
    private TagSystem _tag = default!;

    /// <summary>
    /// Tags to be filtered out
    /// </summary>
    [DataField(required: true)]
    public string TagsKey = default!;

    /// <summary>
    /// Do you need to check for all tags or just one tag
    /// </summary>
    [DataField(required: true)]
    public string RequireAllKey = default!;

    private bool _allTagsRequired;
    private List<ProtoId<TagPrototype>>? _tags;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _tag = entManager.System<TagSystem>();
    }

    public override bool Startup(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(RequireAllKey, out _allTagsRequired, EntityManager)
            || !blackboard.TryGetValue(TagsKey, out List<string>? tags, EntityManager))
            return false;

        _tags = tags.Select(x => (ProtoId<TagPrototype>) x).ToList();
        return true;
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        if (_allTagsRequired && _tag.HasAllTags(uid, _tags!))
            return true;

        if (!_allTagsRequired && _tag.HasAnyTag(uid, _tags!))
            return true;

        return false;
    }
}
