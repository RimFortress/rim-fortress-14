using Content.Shared._RF.NPC.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC.Components;

/// <summary>
/// This is used to indicate ownership relationships between entities.
/// </summary>
[Access(typeof(OwnershipSystem))]
[RegisterComponent, NetworkedComponent]
public sealed partial class OwnershipComponent : Component
{
    /// <summary>
    /// Entities that own this.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> Owners = new();

    /// <summary>
    /// Entities owned by this.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> Owned = new();
}

[Serializable, NetSerializable]
public sealed class OwnershipComponentState(HashSet<NetEntity> owners, HashSet<NetEntity> owned) : ComponentState
{
    public HashSet<NetEntity> Owners = owners;
    public HashSet<NetEntity> Owned = owned;
}
