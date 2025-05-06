using Content.Server.NPC;
using Content.Shared.NPC.Systems;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks whether one entity is friendly to another entity
/// </summary>
public sealed partial class IsFriendlyPrecondition : InvertiblePrecondition
{
    private NpcFactionSystem _faction = default!;

    /// <summary>
    /// A key with an entity that needs to be checked for friendliness to another
    /// </summary>
    [DataField]
    public string Key = NPCBlackboard.Owner;

    /// <summary>
    /// A key with an entity whose friendliness needs to be checked
    /// </summary>
    [DataField(required: true)]
    public string TargetKey = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _faction = sysManager.GetEntitySystem<NpcFactionSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue<EntityUid>(Key, out var entity, EntityManager)
               && blackboard.TryGetValue<EntityUid>(TargetKey, out var target, EntityManager)
               && _faction.IsEntityFriendly(entity, target);
    }
}
