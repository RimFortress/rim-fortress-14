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
    /// The fewest residents that a settlement must have for the event to happen to it. The settlements are of any size, even of one
    /// pop that has gone off on its own, so it is up to the event to say which are not worth it.
    /// </summary>
    [DataField]
    public int MinResidents = 2;

    /// <summary>
    /// The most residents that a settlement can have for the event to happen to it, no limit if it is not set.
    /// </summary>
    [DataField]
    public int? MaxResidents;

    /// <summary>
    /// Cost of the event in points. <seealso cref="NarratorPrototype"/>
    /// </summary>
    [DataField]
    public int Cost = 10;

    /// <summary>
    /// How the rule ended, if it has and the one who ended it said. It is whatever the one who uses the rule expects.
    /// </summary>
    [ViewVariables]
    public string? Result;

    /// <summary>
    /// The settlement that the event is about: the events depend on the settlement alone, not on its player,
    /// whom the event reaches through the settlement when it needs to.
    /// </summary>
    [ViewVariables]
    public EntityUid Settlement;

    [ViewVariables]
    public EntityCoordinates TargetCoordinates;

    /// <summary>
    /// The narrator that started the event (the entity with <see cref="NarratorRuleComponent"/>), whose points the event is made
    /// of. There can be more than one narrator in the world, and each events are its own. Null if no narrator started it.
    /// </summary>
    [ViewVariables]
    public EntityUid? Narrator;
}
