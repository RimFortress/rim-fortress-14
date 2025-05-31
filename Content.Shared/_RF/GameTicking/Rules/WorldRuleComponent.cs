using Content.Shared._RF.Narrator;
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
    public TimeSpan StartOffset = TimeSpan.FromMinutes(10);

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
    /// Cost of the event in points. <seealso cref="NarratorPrototype"/>
    /// </summary>
    [DataField]
    public int Cost = 10;

    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public EntityCoordinates TargetCoordinates;
}
