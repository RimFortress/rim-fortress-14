using Content.Shared.EntityEffects;

namespace Content.Shared._RF.Trigger.Components.Effects;

/// <summary>
/// Applies a list of entity effects when triggered, a different one for every key. <c>EntityEffectOnTrigger</c> has one list
/// for all its keys, and a component can be only once on an entity.
/// </summary>
[RegisterComponent]
public sealed partial class KeyedEntityEffectOnTriggerComponent : Component
{
    /// <summary>
    /// The effects by the key of the trigger. A trigger without a key applies all of them.
    /// </summary>
    [DataField]
    public Dictionary<string, EntityEffect[]> Effects = new();

    /// <summary>
    /// Optional scale multiplier for the effects.
    /// </summary>
    [DataField]
    public float Scale = 1f;

    /// <summary>
    /// Set to true to make the user of the trigger the target of the effects.
    /// Set to false to make the owner of this component the target.
    /// </summary>
    [DataField]
    public bool TargetUser;
}
