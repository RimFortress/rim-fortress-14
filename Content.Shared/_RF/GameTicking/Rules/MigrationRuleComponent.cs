using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.GameTicking.Rules;

/// <summary>
/// Events of refugee migration to the settlement
/// </summary>
[RegisterComponent]
public sealed partial class MigrationRuleComponent : Component
{
    /// <summary>
    /// Minimum and maximum number of mobs to be added
    /// </summary>
    [DataField]
    public MinMax Amount { get; set; } = new(1, 3);

    /// <summary>
    /// Entities to be spawned, the entity is randomly selected from the list
    /// </summary>
    [DataField]
    public List<EntProtoId> Spawn { get; set; } = new();

    /// <summary>
    /// Should spawn entities be added to a player's pops list
    /// </summary>
    [DataField]
    public bool AddToPops { get; set; }
}
