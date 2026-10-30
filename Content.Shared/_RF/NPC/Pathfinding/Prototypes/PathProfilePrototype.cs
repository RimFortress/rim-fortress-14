using Content.Shared._RF.NPC.GOAP;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._RF.NPC.Pathfinding.Prototypes;

/// <summary>
/// A prototype that contains a set of rules describing how pathfinding should occur.
/// </summary>
[Prototype]
public sealed partial class PathProfilePrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <inheritdoc/>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<PathProfilePrototype>))]
    public string[]? Parents { get; set; }

    /// <inheritdoc/>
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; set; }

    /// <summary>
    /// The conditions under which an agent can use this profile.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public List<GoapCondition> Conditions = new();

    /// <summary>
    /// A list of rules describing how to calculate the cost of each tile when finding a path.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public List<PathfindingRule> Rules = new();
}
