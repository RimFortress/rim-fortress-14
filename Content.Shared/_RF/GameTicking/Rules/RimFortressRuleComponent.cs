using Content.Shared.Destructible.Thresholds;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.NPC.Prototypes;
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

    // Yeah, I've made 100 faction prototypes that differ only by the number in the id, so....
    // That's the prefix that starts the ids of all these factions.
    // It's done to avoid messing with the official code.
    /// <summary>
    /// Prototypes of player factions that start out the same and differ only by the numbers at the end
    /// </summary>
    [DataField]
    public string FactionProtoPrefix = "RimFortressFaction";

    /// <summary>
    /// Standard friendly factions for the player
    /// </summary>
    [DataField]
    public List<ProtoId<NpcFactionPrototype>> PlayerFactionFriends = new();

    /// <summary>
    /// Standard hostile factions for the player
    /// </summary>
    [DataField]
    public List<ProtoId<NpcFactionPrototype>> PlayerFactionHostiles = new();

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
    /// Time to the first event on the map, after the establishment of a settlement on the map, in minutes
    /// </summary>
    [DataField]
    public int MinimumTimeUntilFirstEvent = 10;

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
