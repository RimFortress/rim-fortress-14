using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.MathHelpers;
using Content.Shared._RF.Narrator;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.GameTicking.Rules;

/// <summary>
/// Entity migration event to the player's settlement.
/// </summary>
[RegisterComponent]
public sealed partial class MigrationRuleComponent : Component
{
    /// <summary>
    /// Maximum number of entities that can be spawned.
    /// </summary>
    [DataField]
    public int MaxSpawn;

    /// <summary>
    /// Entities to be spawned, the entity is randomly selected from the list.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int> Spawn = new();

    /// <summary>
    /// What can be bought for the entities that come, with the points of the event,
    /// see <see cref="EventBudgetSystem"/>: the gear that makes them stronger. The purchases are in groups,
    /// and each group has its own chance of being chosen, its own limit of how many of it one entity can have
    /// (the armor, the weapons, the tools), and the chances of the purchases in it.
    /// </summary>
    [DataField]
    public List<EventPurchaseGroup> Purchases = new();

    /// <summary>
    /// What part of the points is kept for the <see cref="Purchases"/>, from 0 to 1,
    /// chosen by chance between the two numbers every time. The rest is for the entities that come.
    /// What is left of the points of the entities is spent on the purchases too.
    /// </summary>
    [DataField]
    public MinMaxFloat PurchaseShare = new(0f, 0f);

    /// <summary>
    /// Should spawn entities be added to a player's pops list.
    /// </summary>
    [DataField]
    public bool AddToPops;

    /// <summary>
    /// How far from the settlement the event takes place: the distance is chosen by chance between the two numbers. The minimum is also
    /// how close to any settlement of the player it can be.
    /// </summary>
    [DataField]
    public MinMaxFloat RadiusFromSettlement = new(30f, 45f);

    /// <summary>
    /// Utility AI goal that will be given to entities.
    /// </summary>
    /// <remarks>
    /// The coordinates of one of the player's settlements will be assigned as the task target.
    /// </remarks>
    [DataField]
    public ProtoId<ExecutableGoalPrototype>? Goal;
}
