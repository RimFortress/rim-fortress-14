using Content.Shared.EntityConditions;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Narrator;

/// <summary>
/// A thing that can be bought with the points of an event of the narrator (see <see cref="EventBudgetSystem"/>): what it costs, for whom
/// it is, and what happens to the one it is bought for. It is the one description of a purchase for anything that has event points to
/// spend: the gear of the raiders that a raid is given the points for, the guards of a caravan, the gifts of a trader. How likely it is
/// to be chosen is not up to the purchase, but to the one who buys, in each case, see <see cref="EventPurchaseGroup"/>.
/// </summary>
[Prototype]
public sealed partial class EventPurchasePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// How many event points it costs.
    /// </summary>
    [DataField(required: true)]
    public int Cost;

    /// <summary>
    /// What the one it is bought for has to be like. Anybody is fine if it is not set.
    /// </summary>
    [DataField]
    public EntityCondition[]? Conditions;

    /// <summary>
    /// What happens to the one it is bought for, they are the target of the effects, and the one who buys (the event) is their user.
    /// </summary>
    [DataField]
    public EntityEffect[] Effects = [];
}

/// <summary>
/// A group of the purchases of an event, with its own limit and chance: it is how a set of things that are of a kind (the armor, the
/// weapons, the tools) is told apart from the others, so the limit is on the kind and not on all of them together, and so the chance
/// of getting something of the kind is its own. It is in the event that buys, and the same purchase can be in the groups of different
/// events with different weights.
/// </summary>
[DataDefinition]
public sealed partial class EventPurchaseGroup
{
    /// <summary>
    /// How likely the group is to be chosen among the groups that something can be bought from, the relative weight.
    /// </summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>
    /// The most purchases of the group that one can have. Each one has its own limit, a number from none to this one chosen by chance, so
    /// they do not have the same.
    /// </summary>
    [DataField]
    public int Max = 1;

    /// <summary>
    /// The purchases of the group, and how likely each is to be chosen when the group is, the relative weight.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<EventPurchasePrototype>, float> Purchases = new();
}
