using Robust.Shared.GameStates;

namespace Content.Shared._RF.NPC;

/// <summary>
/// This is used to indicate ownership relationships between entities.
/// </summary>
[Access(typeof(OwnedSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OwnedComponent : Component
{
    /// <summary>
    /// Entities that own this.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Owners = new();

    /// <summary>
    /// Entities owned by this.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Owned = new();
}
