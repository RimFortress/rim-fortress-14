using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.EntityEffects;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Sets a flag in the state of the target, such as the one that the goals of an agent look at to know that it is retreating.
/// </summary>
public sealed partial class SetGoapFlag : EntityEffectBase<SetGoapFlag>
{
    /// <summary>
    /// The key.
    /// </summary>
    [DataField(required: true)]
    public StateKey<bool> Key;

    /// <summary>
    /// The value.
    /// </summary>
    [DataField]
    public bool Value = true;
}

public sealed partial class SetGoapFlagEntityEffectsSystem : EntityEffectSystem<GoapComponent, SetGoapFlag>
{
    [Dependency] private SharedGoapSystem _goap = null!;

    protected override void Effect(Entity<GoapComponent> ent, ref EntityEffectEvent<SetGoapFlag> args)
        => _goap.SetValue(ent.Comp.State, args.Effect.Key, args.Effect.Value);
}
