using Robust.Shared.GameStates;

namespace Content.Shared._RF.World.Settlements;

/// <summary>
/// A settlement of a player: a group of its pops, and the things it owns, that live close to each other. The entity stays the
/// same while the group changes, see <see cref="SettlementTrackerComponent"/>, and it is where the center of the group is.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSettlementSystem))]
public sealed partial class SettlementComponent : Component
{
    /// <summary>
    /// The player entity the settlement belongs to.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid Player;

    /// <summary>
    /// How far from the center the farthest part of the settlement is.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float Radius;

    /// <summary>
    /// How many pops live in the settlement.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int ResidentCount;

    /// <summary>
    /// How many things of the player belong to the settlement.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int OwnedCount;

    /// <summary>
    /// When the settlement appeared.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan Created;

    /// <summary>
    /// The group is gone, and the settlement is waiting to see if it comes back before it is disbanded.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Dissolving;

    /// <summary>
    /// The pops that live in the settlement. Only the server knows them, the client has only their number.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<EntityUid> Residents = new();

    /// <summary>
    /// Since when the residents that are not close to the others have been away. They stay residents for a while.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<EntityUid, TimeSpan> Away = new();

    /// <summary>
    /// When the settlement is disbanded if the group does not come back.
    /// </summary>
    [ViewVariables]
    public TimeSpan? DissolveAt;
}
