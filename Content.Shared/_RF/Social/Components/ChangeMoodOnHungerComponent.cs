using Content.Shared.Nutrition.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Social.Components;

/// <summary>
/// This is used for issuing mood effects when the hunger level of an entity changes
/// </summary>
[RegisterComponent]
public sealed partial class ChangeMoodOnHungerComponent : Component
{
    /// <summary>
    /// Effects that will be given for each hunger level
    /// </summary>
    [DataField]
    public Dictionary<HungerThreshold, List<ProtoId<SocialEffectPrototype>>> Effects = new();

    /// <summary>
    /// Effects that will be removed for each hunger level
    /// </summary>
    [DataField]
    public Dictionary<HungerThreshold, List<ProtoId<SocialEffectPrototype>>> RemoveEffects = new();
}
