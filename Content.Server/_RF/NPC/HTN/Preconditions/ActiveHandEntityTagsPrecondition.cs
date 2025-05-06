using Content.Server.NPC;
using Content.Shared.Hands.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks an entity in the hands of an NPC for the specified tags
/// </summary>
public sealed partial class ActiveHandEntityTagsPrecondition : InvertiblePrecondition
{
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

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _tag = sysManager.GetEntitySystem<TagSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(NPCBlackboard.ActiveHand, out Hand? activeHand, EntityManager)
               && activeHand.HeldEntity != null
               && (!RequireAll || _tag.HasAllTags(activeHand.HeldEntity.Value, Tags))
               && (RequireAll || _tag.HasAnyTag(activeHand.HeldEntity.Value, Tags));
    }
}
