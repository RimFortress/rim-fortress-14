using Content.Shared._RF.Social.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Social.Components;

/// <summary>
/// Stores information about the social interactions of an entity
/// </summary>
[Access(typeof(SocialSystem))]
[RegisterComponent, NetworkedComponent]
public sealed partial class SocialComponent : Component
{
    /// <summary>
    /// Effects on mood
    /// </summary>
    [ViewVariables]
    public Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?> MoodEffects = new();

    /// <summary>
    /// Effects acting on the opinion of an entity to other entities
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?>> OpinionEffects = new();

    [ViewVariables]
    public TimeSpan NexUpdate = TimeSpan.Zero;

    // There is no point in checking everything too often
    public static readonly TimeSpan UpdateRate = TimeSpan.FromSeconds(1);
}

[Serializable, NetSerializable]
public sealed class SocialComponentState(
    Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?> moodEffects,
    Dictionary<NetEntity, Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?>> opinionEffects) : ComponentState
{
    public Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?> MoodEffects = moodEffects;
    public Dictionary<NetEntity, Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?>> OpinionEffects = opinionEffects;
}
