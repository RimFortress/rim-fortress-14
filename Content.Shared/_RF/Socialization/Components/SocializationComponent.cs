using Content.Shared._RF.Socialization.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Socialization.Components;

/// <summary>
/// Stores information about the social interactions of an entity
/// </summary>
[Access(typeof(SocializationSystem))]
[RegisterComponent, NetworkedComponent]
public sealed partial class SocializationComponent : Component
{
    /// <summary>
    /// Effects on mood
    /// </summary>
    [ViewVariables]
    public Dictionary<ProtoId<SocializationEffectPrototype>, TimeSpan?> MoodEffects = new();

    /// <summary>
    /// Effects acting on the opinion of an entity to other entities
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, Dictionary<ProtoId<SocializationEffectPrototype>, TimeSpan?>> OpinionEffects = new();

    [ViewVariables]
    public TimeSpan NexUpdate = TimeSpan.Zero;

    // There is no point in checking everything too often
    public static readonly TimeSpan UpdateRate = TimeSpan.FromSeconds(1);
}

[Serializable, NetSerializable]
public sealed class SocializationComponentState(
    Dictionary<ProtoId<SocializationEffectPrototype>, TimeSpan?> moodEffects,
    Dictionary<NetEntity, Dictionary<ProtoId<SocializationEffectPrototype>, TimeSpan?>> opinionEffects) : ComponentState
{
    public Dictionary<ProtoId<SocializationEffectPrototype>, TimeSpan?> MoodEffects = moodEffects;
    public Dictionary<NetEntity, Dictionary<ProtoId<SocializationEffectPrototype>, TimeSpan?>> OpinionEffects = opinionEffects;
}
