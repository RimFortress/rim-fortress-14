using Content.Shared._RF.Socialization;
using Content.Shared._RF.Socialization.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.EntityEffects;

/// <summary>
/// Adds an effect on the opinion of an entity to any of the participants in the conversation
/// </summary>
public sealed partial class AddOpinionEffect : EntityEffect
{
    /// <summary>
    /// IDs of conversation participants, effect on opinion to which should be added
    /// </summary>
    [DataField]
    public List<string> Actors = new();

    /// <summary>
    /// Prototype of the effect
    /// </summary>
    [DataField]
    public ProtoId<SocializationEffectPrototype> Proto;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectConversationArgs convArgs)
            throw new NotImplementedException();

        var sys = args.EntityManager.System<SocializationSystem>();

        foreach (var actor in Actors)
        {
            if (convArgs.Actors.TryGetValue(actor, out var uid))
                sys.AddOpinionEffect(args.TargetEntity, uid, Proto);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
}
