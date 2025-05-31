using Content.Server._RF.NPC;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Entity migration event to the player's settlement
/// </summary>
[RegisterComponent]
public sealed partial class MigrationRuleComponent : Component
{
    /// <summary>
    /// Maximum number of entities that can be spawned
    /// </summary>
    [DataField]
    public int MaxSpawn;

    /// <summary>
    /// Entities to be spawned, the entity is randomly selected from the list
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int> Spawn { get; set; } = new();

    /// <summary>
    /// Should spawn entities be added to a player's pops list
    /// </summary>
    [DataField]
    public bool AddToPops { get; set; }

    /// <summary>
    /// Minimum radius from the player's settlements where the event will take place
    /// </summary>
    [DataField]
    public int RadiusFromSettlement = 30;

    /// <summary>
    /// NPC task that will be given to entities
    /// </summary>
    /// <remarks>
    /// The coordinates of one of the player's settlements will be assigned as the task target
    /// </remarks>
    [DataField]
    public ProtoId<NpcTaskPrototype>? Task { get; set; }
}
