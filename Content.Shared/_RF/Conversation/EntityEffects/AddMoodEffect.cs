using Content.Shared._RF.Socialization;
using Content.Shared._RF.Socialization.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.EntityEffects;

/// <summary>
/// Adds an effect on the entity's mood
/// </summary>
public sealed partial class AddMoodEffect : EntityEffect
{
    /// <summary>
    /// Prototype of the effect
    /// </summary>
    [DataField]
    public ProtoId<SocializationEffectPrototype> Proto;

    public override void Effect(EntityEffectBaseArgs args)
    {
        args.EntityManager.System<SocializationSystem>().AddMoodEffect(args.TargetEntity, Proto);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
}
