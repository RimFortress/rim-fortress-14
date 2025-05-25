using Content.Shared.Destructible.Thresholds;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.GameTicking.Rules;

/// <summary>
/// A component of a local world event that targets a specific settlement of a specific player
/// </summary>
[RegisterComponent]
public sealed partial class WorldRuleComponent : Component
{
    /// <summary>
    /// The minimum and maximum time between rule starts in seconds.
    /// </summary>
    [DataField]
    public MinMax? Delay;

    /// <summary>
    /// The time that must pass after the player is spawned for the event to be able to happen
    /// </summary>
    [DataField]
    public TimeSpan StartOffset = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Biomes in which the event may occur
    /// </summary>
    [DataField]
    public List<ProtoId<BiomeTemplatePrototype>>? RequiredBiomes;

    /// <summary>
    /// Minimal number of players for this event to start
    /// </summary>
    [DataField]
    public int MinPlayers;

    /// <summary>
    /// Event rarity is -1 to 1, where 1 is a 100% chance of triggering the event
    /// </summary>
    [DataField]
    public float Threshold = -1;

    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public EntityCoordinates TargetCoordinates;
}
