using Content.Shared.Whitelist;

namespace Content.Shared._RF.World.Settlements;

/// <summary>
/// Finds the settlements of a player. The groups of the pops of the player, and of the things it owns, that are close to
/// each other are found every so often, and the settlements that were there before are matched to them, so a settlement
/// stays the same entity while its group changes. A group is a settlement when it has enough pops in it.
/// </summary>
[RegisterComponent]
public sealed partial class SettlementTrackerComponent : Component
{
    /// <summary>
    /// How often the groups are found.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How far from each other two things can be to belong to one group: pops that are farther are in different settlements.
    /// </summary>
    [DataField]
    public float Radius = 100f;

    /// <summary>
    /// Do the things the player owns join the groups. They connect the pops that are far from each other and are a part of
    /// the settlement, but they are not residents and do not make a group a settlement: a group needs a pop.
    /// </summary>
    [DataField]
    public bool CountOwned = true;

    /// <summary>
    /// The things that join the groups, all of them if it is not set.
    /// </summary>
    [DataField]
    public EntityWhitelist? OwnedWhitelist;

    /// <summary>
    /// The things that do not join the groups.
    /// </summary>
    [DataField]
    public EntityWhitelist? OwnedBlacklist;

    /// <summary>
    /// What a pop brings with it into a settlement, to be owned by the settlement with it: whatever the pop carries that fits the
    /// whitelist. The things that are not things (the solutions of the body, the virtual storages) are not owned.
    /// </summary>
    [DataField]
    public EntityWhitelist? ResidentItems;

    /// <summary>
    /// How much a pop weighs when the center of the settlement is found.
    /// </summary>
    [DataField]
    public float PopWeight = 1f;

    /// <summary>
    /// How much a thing the player owns weighs when the center of the settlement is found.
    /// </summary>
    [DataField]
    public float OwnedWeight;

    /// <summary>
    /// How much of the residents of a settlement, or of a group, whichever are fewer, has to be in the other for the group
    /// to be that settlement.
    /// </summary>
    [DataField]
    public float MatchThreshold = 0.3f;

    /// <summary>
    /// How long a resident that has left its group stays a resident. After that it is free to make a settlement of its own, however
    /// few the pops are that are with it.
    /// </summary>
    [DataField]
    public TimeSpan LeaveDelay = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How long a settlement whose group is gone waits for it to come back before it is disbanded.
    /// </summary>
    [DataField]
    public TimeSpan GracePeriod = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How far the center of a group has to move for the settlement to move after it.
    /// </summary>
    [DataField]
    public float MoveThreshold = 1f;

    /// <summary>
    /// When the groups are found next.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextUpdate;

    /// <summary>
    /// The groups are to be found as soon as possible, something has changed.
    /// </summary>
    [ViewVariables]
    public bool SettlementsDirty = true;
}
