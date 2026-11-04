using System.Linq;
using Content.Shared.EntityConditions;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._RF.Narrator;

/// <summary>
/// What was bought for whom, and how many event points are left.
/// </summary>
/// <param name="Left">The points that were not spent.</param>
/// <param name="Bought">The purchases and those they were made for, in the order they were made.</param>
public sealed record EventBudgetResult(int Left, List<(ProtoId<EventPurchasePrototype> Purchase, EntityUid Target)> Bought);

/// <summary>
/// Spending the event points of the narrator: a budget and a list of what can be bought with it, the choice is by the weights among what
/// can be afforded, until nothing can. The same for any event that has points to spend:
/// <list type="bullet">
/// <item><see cref="Buy{T}"/> chooses things for the points, such as who comes (the entities of a migration, each with its price).</item>
/// <item><see cref="Spend"/> buys the purchases (<see cref="EventPurchasePrototype"/>) for the entities that are there, such as the gear
/// of the raiders. The purchases are in groups (<see cref="EventPurchaseGroup"/>), each with its own chance, its own limit for one who
/// is bought for, and the chances of the purchases in it: what it costs, for whom it is, and what it does to them, is in the purchase.</item>
/// </list>
/// </summary>
public sealed class EventBudgetSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private IPrototypeManager _proto = null!;
    [Dependency] private SharedEntityEffectsSystem _effects = null!;
    [Dependency] private SharedEntityConditionsSystem _conditions = null!;

    /// <summary>
    /// Chooses what to buy for the points: by the weights among what can be afforded, again and again, until nothing can or the
    /// number is reached. The points are spent.
    /// </summary>
    /// <param name="points">The points, they are what is left at the end.</param>
    /// <param name="options">What can be bought, with the price and the weight.</param>
    /// <param name="max">The most to buy, no limit if it is 0 or less.</param>
    /// <returns>What was bought, the same thing can be bought many times.</returns>
    [PublicAPI]
    public List<T> Buy<T>(ref int points, IEnumerable<(T Item, int Cost, float Weight)> options, int max = 0)
    {
        var all = options.ToList();
        var bought = new List<T>();

        while (max <= 0 || bought.Count < max)
        {
            var left = points;
            var affordable = all.Where(x => x.Cost <= left && x.Weight > 0f).ToList();

            if (affordable.Count == 0)
                break;

            var chosen = Pick<(T Item, int Cost, float Weight)>(affordable, x => x.Weight);
            bought.Add(chosen.Item);
            points -= chosen.Cost;
        }

        return bought;
    }

    /// <summary>
    /// Buys the purchases of the groups for the targets, until nothing can be bought. Each time a group is chosen by its weight among
    /// those that something can be bought from, then a purchase of it by the weights of the group, among those that can be afforded and
    /// are for somebody, and then a target, by chance, among those whom the purchase is for (its conditions) and who can have more of
    /// the group. Each target has its own limit for each group, a number from none to the <see cref="EventPurchaseGroup.Max"/> of it
    /// chosen by chance, so they do not have the same: some have more, some have none, and the limit is on the kind of the purchases,
    /// not on all of them. The effects are applied to the target.
    /// </summary>
    /// <param name="points">The event points.</param>
    /// <param name="groups">What can be bought.</param>
    /// <param name="targets">Who it is bought for.</param>
    /// <param name="source">The one who buys, such as the event: the user of the effects.</param>
    [PublicAPI]
    public EventBudgetResult Spend(
        int points,
        IReadOnlyList<EventPurchaseGroup> groups,
        IReadOnlyList<EntityUid> targets,
        EntityUid? source = null)
    {
        var result = new EventBudgetResult(points, new());

        // The purchases of the groups, found once.
        var options = groups
            .Select(group => (
                Group: group,
                Purchases: group.Purchases
                    .Where(x => x.Value > 0f)
                    .Select(x => (Purchase: _proto.Index(x.Key), Weight: x.Value))
                    .ToList()))
            .Where(x => x.Group.Weight > 0f && x.Group.Max > 0 && x.Purchases.Count > 0)
            .ToList();

        // How many of each group a target has, and the most that it can.
        var counts = new Dictionary<(EventPurchaseGroup, EntityUid), int>();
        var limits = new Dictionary<(EventPurchaseGroup, EntityUid), int>();

        foreach (var (group, _) in options)
        {
            foreach (var target in targets)
            {
                counts[(group, target)] = 0;
                limits[(group, target)] = _random.Next(group.Max + 1);
            }
        }

        while (true)
        {
            var left = result.Left;

            // For each group, the purchases that can be afforded and are for somebody, with whom they are for.
            var candidates = options
                .Select(x => (
                    x.Group,
                    Purchases: x.Purchases
                        .Where(p => p.Purchase.Cost <= left)
                        .Select(p => (p.Purchase, p.Weight, Targets: Eligible(p.Purchase, x.Group, targets, counts, limits, source)))
                        .Where(p => p.Targets.Count > 0)
                        .ToList()))
                .Where(x => x.Purchases.Count > 0)
                .ToList();

            if (candidates.Count == 0)
                break;

            var chosenGroup = Pick(candidates, x => x.Group.Weight);
            var (purchase, _, eligible) = Pick(chosenGroup.Purchases, x => x.Weight);
            var target = _random.Pick(eligible);

            _effects.ApplyEffects(target, purchase.Effects, 1f, source);
            counts[(chosenGroup.Group, target)]++;
            result.Bought.Add((purchase.ID, target));
            result = result with { Left = result.Left - purchase.Cost };
        }

        return result;
    }

    private List<EntityUid> Eligible(
        EventPurchasePrototype purchase,
        EventPurchaseGroup group,
        IReadOnlyList<EntityUid> targets,
        Dictionary<(EventPurchaseGroup, EntityUid), int> counts,
        Dictionary<(EventPurchaseGroup, EntityUid), int> limits,
        EntityUid? source)
        => targets
            .Where(x => !TerminatingOrDeleted(x)
                        && counts[(group, x)] < limits[(group, x)]
                        && _conditions.TryConditions(x, purchase.Conditions, source))
            .ToList();

    private T Pick<T>(IReadOnlyList<T> options, Func<T, float> weight)
    {
        var total = options.Sum(weight);
        var roll = _random.NextFloat() * total;

        foreach (var option in options)
        {
            roll -= weight(option);

            if (roll <= 0f)
                return option;
        }

        return options[^1];
    }
}
