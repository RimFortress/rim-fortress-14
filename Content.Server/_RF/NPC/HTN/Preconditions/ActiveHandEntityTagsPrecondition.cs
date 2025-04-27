using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Hands.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks an entity in the hands of an NPC for the specified tags
/// </summary>
public sealed partial class ActiveHandEntityTagsPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private TagSystem _tag = default!;

    /// <summary>
    /// Tags to check the entity for
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<TagPrototype>> Tags = new();

    /// <summary>
    /// Need to check for all tags or just one of any tag
    /// </summary>
    [DataField]
    public bool RequireAll = true;

    [DataField]
    public bool Invert;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _tag = sysManager.GetEntitySystem<TagSystem>();
    }

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(NPCBlackboard.ActiveHand, out Hand? activeHand, _entManager)
            || activeHand.HeldEntity == null)
            return Invert;

        if (RequireAll && !_tag.HasAllTags(activeHand.HeldEntity.Value, Tags))
            return Invert;

        if (!RequireAll && !_tag.HasAnyTag(activeHand.HeldEntity.Value, Tags))
            return Invert;

        return !Invert;
    }
}
