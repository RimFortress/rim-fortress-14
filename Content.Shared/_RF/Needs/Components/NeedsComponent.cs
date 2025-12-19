using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Needs.Components;

/// <summary>
/// This is used for the needs of the entity that need to be fulfilled
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NeedsComponent : Component
{
    /// <summary>
    /// A list with data about all the needs of the entity
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public List<NeedData> Needs = new();
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class NeedData
{
    /// <summary>
    /// The prototype of the need
    /// </summary>
    [DataField]
    public ProtoId<NeedPrototype> Id;

    /// <summary>
    /// The need value as authoritatively set by the server as of <see cref="LastAuthoritativeChangeTime"/>.
    /// This value should be updated relatively infrequently.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float LastAuthoritativeValue;

    /// <summary>
    /// The time at which <see cref="LastAuthoritativeValue"/> was last updated.
    /// </summary>
    [DataField, ViewVariables]
    public TimeSpan LastAuthoritativeChangeTime;

    /// <summary>
    /// The actual amount at which <see cref="LastAuthoritativeValue"/> decays.
    /// Affected by <seealso cref="CurrentThreshold"/>
    /// </summary>
    [DataField, ViewVariables]
    public float ActualDecayRate;

    /// <summary>
    /// The last threshold this entity was at.
    /// Stored in order to prevent recalculating
    /// </summary>
    [DataField, ViewVariables]
    public string LastThreshold = string.Empty;

    /// <summary>
    /// The time when the threshold will update next
    /// </summary>
    [DataField, ViewVariables]
    public TimeSpan NextThresholdUpdateTime;

    /// <summary>
    /// The current level of satisfaction threshold the entity is at
    /// </summary>
    [DataField, ViewVariables]
    public string CurrentThreshold = string.Empty;
}
