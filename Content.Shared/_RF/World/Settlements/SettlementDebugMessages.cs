using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.World.Settlements;

/// <summary>
/// Asks the server to send, or to stop sending, the snapshots of the settlements for the debug overlay. Only for those who may debug.
/// </summary>
[Serializable, NetSerializable]
public sealed class SettlementsDebugRequest(bool enabled) : EntityEventArgs
{
    public readonly bool Enabled = enabled;
}

/// <summary>
/// Everything about the settlements that the debug overlay shows, as the server sees it: the client sees only the entities that are near
/// it, and does not know what is owned by whom, so the server says it.
/// </summary>
[Serializable, NetSerializable]
public sealed class SettlementsDebugMessage(List<SettlementDebugInfo> settlements, List<SettlementDebugEntity> waiting) : EntityEventArgs
{
    public readonly List<SettlementDebugInfo> Settlements = settlements;

    /// <summary>
    /// What waits with the players for a settlement: it is owned by a player and not by a settlement.
    /// </summary>
    public readonly List<SettlementDebugEntity> Waiting = waiting;
}

/// <summary>
/// A settlement for the debug overlay.
/// </summary>
/// <param name="Uid">The settlement.</param>
/// <param name="Player">The player of the settlement.</param>
/// <param name="PlayerName">The name of the player, if it is known.</param>
/// <param name="Coordinates">The center.</param>
/// <param name="Radius">How far from the center the things of the group are.</param>
/// <param name="Reach">How far from the center a thing is adopted by the settlement.</param>
/// <param name="Residents">The number of the residents.</param>
/// <param name="Owned">The number of all that it owns, with what the pops carry.</param>
/// <param name="Dissolving">It has no group and waits to see if the group is back.</param>
/// <param name="DissolveIn">How many seconds are left until it is disbanded, if it waits.</param>
/// <param name="Wealth">The price of what it owns, by the categories.</param>
/// <param name="EventPoints">The points of the first narrator for its events, -1 if there is no narrator.</param>
/// <param name="WaitPoints">The points of waiting for an event.</param>
/// <param name="BonusPoints">The points that were added to it by hand.</param>
/// <param name="Members">What it owns and is on the ground: the residents, the things, the buildings, the zones.</param>
[Serializable, NetSerializable]
public sealed record SettlementDebugInfo(
    NetEntity Uid,
    NetEntity Player,
    string PlayerName,
    NetCoordinates Coordinates,
    float Radius,
    float Reach,
    int Residents,
    int Owned,
    bool Dissolving,
    float DissolveIn,
    Dictionary<string, double> Wealth,
    int EventPoints,
    int WaitPoints,
    int BonusPoints,
    List<SettlementDebugEntity> Members);

/// <summary>
/// An entity that is owned, for the debug overlay.
/// </summary>
/// <param name="Uid">The entity.</param>
/// <param name="Coordinates">Where it is.</param>
/// <param name="Category">The category of its wealth, or Other.</param>
/// <param name="Away">For a resident that has left the group of its settlement: for how many seconds it has been away, -1 if not.</param>
[Serializable, NetSerializable]
public readonly record struct SettlementDebugEntity(NetEntity Uid, NetCoordinates Coordinates, string Category, float Away = -1f);
