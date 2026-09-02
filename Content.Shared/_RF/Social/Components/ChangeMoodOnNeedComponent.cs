using Content.Shared._RF.Needs;
using Content.Shared._RF.Needs.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Social.Components;

/// <summary>
/// This is used for issuing mood effects when the need threshold of an entity changes
/// </summary>
[RegisterComponent]
public sealed partial class ChangeMoodOnNeedComponent : Component
{
    /// <summary>
    /// Effects that will be given for each need threshold
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<NeedPrototype>, Dictionary<string, List<ProtoId<SocialEffectPrototype>>>> Effects = new();

    /// <summary>
    /// Effects that will be removed for each need threshold
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<NeedPrototype>, Dictionary<string, List<ProtoId<SocialEffectPrototype>>>> RemoveEffects = new();
}
