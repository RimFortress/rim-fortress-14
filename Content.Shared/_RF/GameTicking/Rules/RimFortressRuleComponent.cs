using Content.Shared._RF.Narrator;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Parallax.Biomes;
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
    /// List of global world events that can happen in a round, with their value in points
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int>? GlobalEvents;

    /// <summary>
    /// A narrator controlling the events of the world
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NarratorPrototype> Narrator;

    [ViewVariables]
    public TimeSpan LastEventTime;

    [ViewVariables]
    public int LastWaitPoints;
}
