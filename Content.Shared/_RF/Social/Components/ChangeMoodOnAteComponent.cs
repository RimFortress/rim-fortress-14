using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Social.Components;

/// <summary>
/// This is used for giving mood effects depending on the food eaten
/// </summary>
[RegisterComponent]
public sealed partial class ChangeMoodOnAteComponent : Component
{
    /// <summary>
    /// Effects with a whitelist that the eaten entity must complete
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SocialEffectPrototype>, EntityWhitelist> Effects = new();

    /// <summary>
    /// Should we give all the suitable effects, or just the first one
    /// </summary>
    [DataField]
    public bool FirstSuitable = true;
}
