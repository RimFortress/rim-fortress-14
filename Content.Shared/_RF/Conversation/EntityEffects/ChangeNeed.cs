using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.MathHelpers;
using Content.Shared._RF.Needs;
using Content.Shared._RF.Needs.Prototypes;
using Content.Shared._RF.Needs.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

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

    /// <summary>
    /// The range from which a random number will be selected and within which the need value will change.
    /// </summary>
    [DataField]
    public MinMaxFloat? Random;
}

public sealed partial class ChangeNeedEntityEffectsSystem : EntityEffectSystem<ConversationActorComponent, ChangeNeed>
{
    [Dependency] private NeedsSystem _needs = default!;
    [Dependency] private IRobustRandom _random = default!;

    protected override void Effect(Entity<ConversationActorComponent> ent, ref EntityEffectEvent<ChangeNeed> args)
    {
        _needs.AddValue(ent.Owner, args.Effect.Need, args.Effect.Amount);

        if (args.Effect.Random is { } minMax)
            _needs.AddValue(ent.Owner, args.Effect.Need, minMax.Next(_random));
    }
}
