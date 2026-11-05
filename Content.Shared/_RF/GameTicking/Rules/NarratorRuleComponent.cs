using Content.Shared._RF.Narrator;
using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.GameTicking.Rules;

/// <summary>
/// A rule that can create other rules for player settlements and the entire world, guided by the narrator's logic.
/// </summary>
[RegisterComponent]
public sealed partial class NarratorRuleComponent : Component
{
    /// <summary>
    /// A narrator controlling the events of the world.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NarratorPrototype> Narrator;

    /// <summary>
    /// Table with events that may occur for each individual player settlement.
    /// </summary>
    [DataField]
    public EntityTableSelector? WorldEvents;

    /// <summary>
    /// Table with global events that can happen on the world map.
    /// </summary>
    [DataField]
    public EntityTableSelector? GlobalEvents;

    /// <summary>
    /// How often the narrator looks at the world: the events are not rolled every tick, but once in this time.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public TimeSpan LastEventTime;

    [ViewVariables]
    public int LastWaitPoints;
}
