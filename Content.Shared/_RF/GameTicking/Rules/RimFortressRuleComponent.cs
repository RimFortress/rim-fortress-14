using Content.Shared.Destructible.Thresholds;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Random;
using Content.Shared.Roles;
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
    /// Biome template that will be used in the creation of the world
    /// </summary>
    [DataField(required: true)]
    public ProtoId<BiomeTemplatePrototype> Biome;

    /// <summary>
    /// Duration of the day
    /// </summary>
    [DataField]
    public TimeSpan DayDuration = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Basic role for settlers unless another is obtained
    /// </summary>
    [DataField(required: true)]
    public ProtoId<JobPrototype> DefaultPopsJob;

    /// <summary>
    /// Components that will be added to the pops when spawned
    /// </summary>
    [DataField]
    public ComponentRegistry? PopsComponentsOverride = new();

    /// <summary>
    /// Table with random events that can happen on the world map
    /// </summary>
    [DataField]
    public EntityTableSelector? WorldEvents;

    /// <summary>
    /// А list of global world events with chances of starting them
    /// </summary>
    [DataField]
    public ProtoId<WeightedRandomPrototype>? GlobalEvents;

    /// <summary>
    /// Minimum and maximum amount of time in seconds between global world events
    /// </summary>
    [DataField]
    public MinMax MinMaxEventTiming;

    [ViewVariables]
    public TimeSpan NextEventTime;
}
