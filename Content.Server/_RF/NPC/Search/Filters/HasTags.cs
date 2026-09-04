using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Checks the tags of an entity.
/// </summary>
public sealed partial class HasTags : BaseSearchFilter<HasTags>
{
    /// <summary>
    /// Tags list.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<TagPrototype>> Tags = new();

    /// <summary>
    /// The check will verify whether all tags are present or just one.
    /// </summary>
    [DataField]
    public bool RequireAll;
}

public sealed partial class HasTagsFilterSystem : NpcSearchFilterSystem<HasTags>
{
    [Dependency] private TagSystem _tag = default!;

    protected override bool Filter(GoapState state, EntityUid target, HasTags filter)
        => filter.RequireAll
            ? _tag.HasAllTags(target, filter.Tags)
            : _tag.HasAnyTag(target, filter.Tags);
}
