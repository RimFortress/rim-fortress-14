using Content.Shared.Destructible.Thresholds;
using Content.Shared.Parallax.Biomes;
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

    /// <summary>
    /// On maps with only these templates, this event can happen.
    /// If not set, no check is performed
    /// </summary>
    [DataField]
    public List<ProtoId<BiomeTemplatePrototype>> RequiredBiomes { get; set; } = new();

    /// <summary>
    /// Size of the chunk in which entities will spawn.
    /// Larger the chunk the greater the chance that entities will be far away from each other,
    /// but smaller chance that the spawn point will be blocked
    /// </summary>
    [DataField]
    public int ChunkSize { get; set; } = 3;
}
