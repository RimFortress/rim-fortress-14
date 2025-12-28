using Content.Shared._RF.Social;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.GameTicking.Rules;

/// <summary>
/// The event of issuing mood effects to all the player's pops
/// </summary>
[RegisterComponent]
public sealed partial class ChangeMoodRuleComponent : Component
{
    /// <summary>
    /// Effects that will be issued
    /// </summary>
    [DataField]
    public List<ProtoId<SocialEffectPrototype>> Effects = new();

    /// <summary>
    /// Effects that will be removed
    /// </summary>
    [DataField]
    public List<ProtoId<SocialEffectPrototype>> RemoveEffects = new();
}
