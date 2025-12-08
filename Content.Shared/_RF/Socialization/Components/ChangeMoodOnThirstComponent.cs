using Content.Shared.Nutrition.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Socialization.Components;

/// <summary>
/// This is used for issuing mood effects when the thirst level of an entity changes
/// </summary>
[RegisterComponent]
public sealed partial class ChangeMoodOnThirstComponent : Component
{
    /// <summary>
    /// Effects that will be given for each thirst level
    /// </summary>
    [DataField]
    public Dictionary<ThirstThreshold, List<ProtoId<SocializationEffectPrototype>>> Effects = new();
}
