using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Socialization;

/// <summary>
/// Stores information about the social interactions of an entity
/// </summary>
[Access(typeof(SocializationSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class SocializationComponent : Component
{
    [DataField]
    public int MaxMood = 50;

    [DataField]
    public int MinMood = -50;

    [DataField]
    public int MaxOpinion = 100;

    [DataField]
    public int MinOpinion = -100;

    /// <summary>
    /// Effects on mood
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<(ProtoId<MoodEffectPrototype> Proto, TimeSpan? EndAt)> MoodEffects = new();

    /// <summary>
    /// Effects acting on the opinion of an entity to other entities
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<EntityUid, List<(ProtoId<OpinionEffectsPrototype> Proto, TimeSpan? EndAt)>> OpinionEffects = new();

    [ViewVariables]
    public TimeSpan NexUpdate = TimeSpan.Zero;

    // There is no point in checking everything too often
    public static readonly TimeSpan UpdateRate = TimeSpan.FromSeconds(1);
}
