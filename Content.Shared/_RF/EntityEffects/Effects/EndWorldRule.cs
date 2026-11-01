using Content.Shared.EntityEffects;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Ends a world rule, and says how it has ended. The target of the effect is the rule entity.
/// </summary>
public sealed partial class EndWorldRule : EntityEffectBase<EndWorldRule>
{
    /// <summary>
    /// How the rule ends, it is whatever the one who uses the rule expects.
    /// The rule is triggered with the keys <c>ended</c> and <c>ended.RESULT</c>.
    /// </summary>
    [DataField]
    public string? Result;
}
