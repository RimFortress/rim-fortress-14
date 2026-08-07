using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.Needs;
using Content.Shared._RF.Needs.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.EntityEffects;

/// <summary>
/// Changes the level of satisfaction of an entity's need.
/// </summary>
public sealed partial class ChangeNeed : EntityEffectBase<ChangeNeed>
{
    /// <summary>
    /// Need prototype.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NeedPrototype> Need;

    /// <summary>
    /// How much will the value be increased.
    /// </summary>
    [DataField]
    public float Amount;
}

public sealed class ChangeNeedEntityEffectsSystem : EntityEffectSystem<ConversationActorComponent, ChangeNeed>
{
    [Dependency] private readonly NeedsSystem _needs = default!;

    protected override void Effect(Entity<ConversationActorComponent> ent, ref EntityEffectEvent<ChangeNeed> args)
    {
        _needs.AddValue(ent.Owner, args.Effect.Need, args.Effect.Amount);
    }
}
