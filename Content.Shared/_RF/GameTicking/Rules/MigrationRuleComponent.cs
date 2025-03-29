using Robust.Shared.Prototypes;

namespace Content.Shared._RF.GameTicking.Rules;

/// <summary>
/// Events of refugee migration to the settlement
/// </summary>
[RegisterComponent]
public sealed partial class MigrationRuleComponent : Component
{
    /// <summary>
    /// Minimum number of settlers to be added
    /// </summary>
    [DataField]
    public int Min { get; set; } = 1;

    /// <summary>
    /// Maximum number of settlers to be added
    /// </summary>
    [DataField]
    public int Max { get; set; } = 3;

    /// <summary>
    /// Entities to be spawned, the entity is randomly selected from the list
    /// </summary>
    [DataField]
    public List<EntProtoId> Spawn { get; set; } = new();

    /// <summary>
    /// Should spawn entities be added to a player's faction
    /// </summary>
    [DataField]
    public bool AddToPlayerFaction { get; set; }
}
