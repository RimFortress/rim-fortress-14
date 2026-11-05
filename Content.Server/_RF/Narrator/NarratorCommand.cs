using System.Linq;
using Content.Server.Administration;
using Content.Server._RF.World.Settlements;
using Content.Shared._RF.Narrator;
using Content.Shared._RF.World.Settlements;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.Narrator;

/// <summary>
/// The commands for debugging the narrator: to look at what it counts, to add the points to the settlements and to make the events
/// happen, to try the purchases of the events. The settlements are piped in (<c>narrator:settlements</c> gives all of them, a player
/// gives its own), the entities of the purchases are piped in as well.
/// </summary>
/// <example>
/// <code>
/// narrator:settlements narrator:info
/// players:self narrator:settlements narrator:points 100 narrator:roll Raid
/// ent 123 narrator:buy RaidKitArmor
/// </code>
/// </example>
[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class NarratorCommand : ToolshedCommand
{
    private NarratorSystem? _narrator;
    private SettlementSystem? _settlements;
    private SettlementWealthSystem? _wealth;
    private EventBudgetSystem? _budget;

    #region Settlements

    /// <summary>
    /// All the settlements.
    /// </summary>
    [CommandImplementation("settlements")]
    public IEnumerable<EntityUid> Settlements()
    {
        _settlements ??= GetSys<SettlementSystem>();
        return _settlements.AllSettlements().Select(x => x.Owner);
    }

    /// <summary>
    /// The settlements of the players.
    /// </summary>
    [CommandImplementation("settlements")]
    public IEnumerable<EntityUid> Settlements([PipedArgument] IEnumerable<EntityUid> players)
    {
        _narrator ??= GetSys<NarratorSystem>();
        return _narrator.ResolveSettlements(players).Select(x => x.Owner);
    }

    #endregion

    #region Looking

    /// <summary>
    /// What the narrators count for the events of the whole world.
    /// </summary>
    [CommandImplementation("global")]
    public string Global()
    {
        _narrator ??= GetSys<NarratorSystem>();
        return _narrator.DebugTextGlobal();
    }

    /// <summary>
    /// What the narrators count for the settlements: the wealth, the points, the events that can happen.
    /// </summary>
    [CommandImplementation("info")]
    public IEnumerable<string> Info([PipedArgument] IEnumerable<EntityUid> settlements)
    {
        _narrator ??= GetSys<NarratorSystem>();
        return _narrator.ResolveSettlements(settlements).Select(x => _narrator.DebugText(x));
    }

    /// <summary>
    /// Counts the wealth of the settlements anew, and says it by the categories.
    /// </summary>
    [CommandImplementation("wealth")]
    public IEnumerable<string> Wealth([PipedArgument] IEnumerable<EntityUid> settlements)
    {
        _narrator ??= GetSys<NarratorSystem>();
        _wealth ??= GetSys<SettlementWealthSystem>();

        foreach (var settlement in _narrator.ResolveSettlements(settlements))
        {
            if (!EntityManager.TryGetComponent(settlement, out SettlementWealthComponent? wealth))
                continue;

            // Counted anew, to see what is there now and not what was there a while ago.
            _wealth.Recount((settlement, wealth));
            var text = $"{EntityManager.ToPrettyString(settlement)}: {_wealth.GetTotal((settlement, wealth)):0}";

            foreach (var (category, value) in wealth.Values)
            {
                text += $"\n  {category}: {value:0} ({wealth.Counts.GetValueOrDefault(category)})";
            }

            yield return text;
        }
    }

    #endregion

    #region Points and events

    /// <summary>
    /// Adds points to the settlements, to be spent on the events. They are taken away by a negative number.
    /// </summary>
    [CommandImplementation("points")]
    public IEnumerable<EntityUid> Points([PipedArgument] IEnumerable<EntityUid> settlements, int amount)
    {
        _narrator ??= GetSys<NarratorSystem>();

        foreach (var settlement in _narrator.ResolveSettlements(settlements))
        {
            _narrator.AddBonusPoints(settlement, amount);
            yield return settlement;
        }
    }

    /// <summary>
    /// Makes the settlements have waited for their events for so many seconds longer (shorter, if it is negative).
    /// </summary>
    [CommandImplementation("wait")]
    public IEnumerable<EntityUid> Wait([PipedArgument] IEnumerable<EntityUid> settlements, float seconds)
    {
        _narrator ??= GetSys<NarratorSystem>();

        foreach (var settlement in _narrator.ResolveSettlements(settlements))
        {
            _narrator.AddWaitTime(settlement, TimeSpan.FromSeconds(seconds));
            yield return settlement;
        }
    }

    /// <summary>
    /// Starts the event for the settlements at once: the chance of it is not asked, nor are the points.
    /// </summary>
    [CommandImplementation("roll")]
    public IEnumerable<EntityUid> Roll([PipedArgument] IEnumerable<EntityUid> settlements, EntProtoId proto)
    {
        _narrator ??= GetSys<NarratorSystem>();

        foreach (var settlement in _narrator.ResolveSettlements(settlements))
        {
            if (_narrator.Roll(settlement, proto) != null)
                yield return settlement;
        }
    }

    /// <summary>
    /// Starts an event that the narrator can afford for the settlements at once, the narrator chooses it: the chance is not asked.
    /// </summary>
    [CommandImplementation("rollany")]
    public IEnumerable<EntityUid> RollAny([PipedArgument] IEnumerable<EntityUid> settlements)
    {
        _narrator ??= GetSys<NarratorSystem>();

        foreach (var settlement in _narrator.ResolveSettlements(settlements))
        {
            if (_narrator.Roll(settlement) != null)
                yield return settlement;
        }
    }

    #endregion

    #region Purchases

    /// <summary>
    /// The purchases that the events can make.
    /// </summary>
    [CommandImplementation("purchases")]
    public IEnumerable<string> Purchases() => GetSys<EventBudgetSystem>()
        .GetPurchases()
        .OrderBy(x => x.ID)
        .Select(x => $"{x.ID}: cost {x.Cost}, {x.Effects.Length} effects");

    /// <summary>
    /// Applies the purchase to the entities, no points are spent. The ones that it does not fit are not returned.
    /// </summary>
    [CommandImplementation("buy")]
    public IEnumerable<EntityUid> Buy([PipedArgument] IEnumerable<EntityUid> entities, ProtoId<EventPurchasePrototype> purchase)
    {
        _budget ??= GetSys<EventBudgetSystem>();
        var proto = _budget.GetPurchase(purchase);

        foreach (var entity in entities)
        {
            if (_budget.Apply(proto, entity))
                yield return entity;
        }
    }

    /// <summary>
    /// Spends the points on the purchases of the event for the entities, as the event does when it comes, and says what was bought.
    /// </summary>
    [CommandImplementation("spend")]
    public IEnumerable<string> Spend([PipedArgument] IEnumerable<EntityUid> entities, int points, EntProtoId rule)
    {
        _budget ??= GetSys<EventBudgetSystem>();

        if (!_budget.TryGetPurchaseGroups(rule, out var groups))
        {
            yield return $"{rule} has no purchases";
            yield break;
        }

        var result = _budget.Spend(points, groups, entities.ToList());

        foreach (var (purchase, target) in result.Bought)
        {
            yield return $"{purchase} for {EntityManager.ToPrettyString(target)}";
        }

        yield return $"{result.Bought.Count} bought, {result.Left} points left";
    }

    #endregion
}
