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
    /// <summary>
    /// Effects on mood
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<SocializationEffect> MoodEffects = new();

    /// <summary>
    /// Effects acting on the opinion of an entity to other entities
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<EntityUid, List<SocializationEffect>> OpinionEffects = new();

    [ViewVariables]
    public TimeSpan NexUpdate = TimeSpan.Zero;

    // There is no point in checking everything too often
    public static readonly TimeSpan UpdateRate = TimeSpan.FromSeconds(1);
}

[DataDefinition]
public sealed partial class SocializationEffect
{
    [DataField]
    public ProtoId<SocializationEffectPrototype> Id;

    [DataField]
    public int Multiplier = 1;

    [DataField]
    public TimeSpan? EndAt;

    public SocializationEffect(
        ProtoId<SocializationEffectPrototype> id,
        int multiplier = 1,
        TimeSpan? endAt = null)
    {
        Id = id;
        Multiplier = multiplier;
        EndAt = endAt;
    }
}
