using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.GOAP.Conditions;

/// <summary>
/// Checks the tags of the target entity.
/// </summary>
public sealed partial class HasTags : BaseGoapCondition<HasTags>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> Target;

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

public sealed class HasTagsSystem : GoapConditionSystem<HasTags>
{
    [Dependency] private readonly TagSystem _tag = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, HasTags condition)
    {
        if (!TryGet(state, condition.Target, out var target))
            return false;

        return condition.RequireAll
            ? _tag.HasAllTags(target, condition.Tags)
            : _tag.HasAnyTag(target, condition.Tags);
    }
}
