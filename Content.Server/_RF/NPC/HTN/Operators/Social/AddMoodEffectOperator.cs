using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Operators.Social;

/// <summary>
/// Adds a mood effect to the target entity
/// </summary>
public sealed partial class AddMoodEffectOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private SocialSystem _social;

    /// <summary>
    /// The key with the target entity
    /// </summary>
    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    /// <summary>
    /// The effect that will be given
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SocialEffectPrototype> Effect;

    /// <summary>
    /// If false, the effect will be given only if the entity does not already have it
    /// </summary>
    [DataField]
    public bool Multiply;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _social = sysManager.GetEntitySystem<SocialSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var uid, _entity))
            return HTNOperatorStatus.Failed;

        if (!Multiply && _social.HasMoodEffect(uid, Effect))
            return HTNOperatorStatus.Finished;

        _social.AddMoodEffect(uid, Effect);
        return HTNOperatorStatus.Finished;
    }
}
