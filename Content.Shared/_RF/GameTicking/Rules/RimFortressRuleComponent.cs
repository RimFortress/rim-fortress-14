using Content.Shared.Destructible.Thresholds;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.GameTicking.Rules;

/// <summary>
/// Basic game rule of RimFortress
/// </summary>
[RegisterComponent]
public sealed partial class RimFortressRuleComponent : Component
{
    /// <summary>
    /// Prototype of the entity the player will move into after entering a round
    /// </summary>
    [DataField]
    public EntProtoId PlayerProtoId = "RimFortressObserver";

    /// <summary>
    /// Number of chunks from the center, on which the planet will be loaded.
    /// Beyond this distance, a border will be created
    /// </summary>
    /// <remarks>
    /// The number of chunks equals: (MaxPlanetChunkDistance * 2 + 1) ^ 2
    /// </remarks>
    [DataField]
    public int PlanetChunkLoadDistance = 6;

    /// <summary>
    /// The prototype that will be used to create the map border
    /// </summary>
    [DataField]
    public EntProtoId PlanetBorderProtoId = "GhostImpassableWall";

    /// <summary>
    /// The size of the RimFortress world, determines the number of possible player maps
    /// </summary>
    [DataField]
    public Vector2i WorldSize = new(10, 10);

    /// <summary>
    /// Biome templates that will be used in the creation of the world
    /// </summary>
    [DataField(required: true), ValidatePrototypeId<WeightedRandomPrototype>]
    public ProtoId<WeightedRandomPrototype> BiomeSet;

    /// <summary>
    /// Duration of the day
    /// </summary>
    [DataField]
    public TimeSpan DayDuration = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Prototypes of entities that may join the settlement as refugees or appear at the roundstart
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> PopsProtoIds = new();

    /// <summary>
    /// Number of settlers at the roundstart
    /// </summary>
    [DataField]
    public int RoundstartPops = 5;

    /// <summary>
    /// The area around the center of the map where settlers can appear
    /// </summary>
    [DataField]
    public float RoundStartSpawnRadius = 20f;

    /// <summary>
    /// The time from the start of the round after which the settlers will fall asleep.
    /// It is necessary to give time for the map to load and for the spawning to work properly.
    /// </summary>
    [DataField]
    public TimeSpan MinimumTimeUntilFirstEvent = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Table with random events that can happen on the world map
    /// </summary>
    [DataField(required: true)]
    public EntityTableSelector WorldEvents = default!;

    /// <summary>
    /// The minimum and maximum time between rule starts in seconds.
    /// </summary>
    [DataField]
    public MinMax MinMaxEventTiming = new(3 * 60, 10 * 60);
}
