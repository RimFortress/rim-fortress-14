using Content.Server.NPC;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Preconditions.Social;

/// <summary>
/// Checks if there is a target effect on the mood of the target entity
/// </summary>
public sealed partial class HasMoodEffectPrecondition : InvertiblePrecondition
{
    private SocialSystem _social;

    /// <summary>
    /// The key with the target entity
    /// </summary>
    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    /// <summary>
    /// The effect to check for
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SocialEffectPrototype> Effect;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _social = sysManager.GetEntitySystem<SocialSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
        => blackboard.TryGetValue<EntityUid>(TargetKey, out var uid, EntityManager)
           && _social.HasMoodEffect(uid, Effect);
}
