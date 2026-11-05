using System.Linq;
using System.Text;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World.Settlements;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Narrator;

/// <summary>
/// What the commands for debugging the narrator (see <see cref="NarratorCommand"/>) use: the narrators that are there, the settlements
/// that are told, the text of what the narrator counts, the points that are added and the events that are made to happen.
/// </summary>
public sealed partial class NarratorSystem
{
    /// <summary>
    /// All the narrators that are in charge of the world now.
    /// </summary>
    public List<Entity<NarratorRuleComponent>> ActiveNarrators()
    {
        var result = new List<Entity<NarratorRuleComponent>>();
        var query = EntityQueryEnumerator<NarratorRuleComponent, ActiveGameRuleComponent>();

        while (query.MoveNext(out var uid, out var comp, out _))
        {
            result.Add((uid, comp));
        }

        return result;
    }

    /// <summary>
    /// The settlements that the entities are: a settlement is itself, a player is all of its settlements.
    /// </summary>
    public List<Entity<SettlementComponent>> ResolveSettlements(IEnumerable<EntityUid> uids)
    {
        var result = new List<Entity<SettlementComponent>>();

        foreach (var uid in uids)
        {
            if (TryComp(uid, out SettlementComponent? settlement))
                result.Add((uid, settlement));
            else
                result.AddRange(_settlements.GetPlayerSettlements(uid));
        }

        return result;
    }

    /// <summary>
    /// What every narrator counts for the settlement.
    /// </summary>
    public string DebugText(Entity<SettlementComponent> settlement)
    {
        var text = new StringBuilder();

        foreach (var narrator in ActiveNarrators())
        {
            text.Append(DebugText(settlement, narrator.Comp));
        }

        return text.ToString();
    }

    /// <summary>
    /// What every narrator counts for the events of the whole world.
    /// </summary>
    public string DebugTextGlobal()
        => string.Concat(ActiveNarrators().Select(x => DebugTextGlobal(x.Comp)));

    /// <summary>
    /// The world events there are, going on and ended: what each is, where, in what phase, how it ended, how many have come.
    /// </summary>
    public IEnumerable<string> DescribeEvents()
    {
        var query = EntityQueryEnumerator<WorldRuleComponent>();

        while (query.MoveNext(out var uid, out var rule))
        {
            var state = HasComp<ActiveGameRuleComponent>(uid)
                ? "ACTIVE"
                : HasComp<EndedGameRuleComponent>(uid)
                    ? $"ended ({rule.Result ?? "no result"})"
                    : "waiting to start";

            var came = _ownership.GetOwned(uid).Count(HasComp<MobStateComponent>);
            var settlement = rule.Settlement.IsValid() && !TerminatingOrDeleted(rule.Settlement)
                ? $"{ToPrettyString(rule.Settlement)} at {_settlements.GetCoordinates(rule.Settlement)}"
                : $"{rule.Settlement} (gone), was at {rule.TargetCoordinates}";

            var narrator = rule.Narrator is { } n && !TerminatingOrDeleted(n) ? ToPrettyString(n) : "none";

            yield return $"{MetaData(uid).EntityPrototype?.ID} ({uid.Id}): {state}, phase {_phases.GetPhase(uid) ?? "-"}, " +
                         $"settlement {settlement}, narrator {narrator}, {came} came";
        }
    }

    /// <summary>
    /// Adds points to a settlement, to be spent on its events. A negative number takes them away.
    /// </summary>
    public int AddBonusPoints(Entity<SettlementComponent> settlement, int amount)
    {
        if (!_eventsQuery.TryComp(settlement, out var events))
            return 0;

        events.BonusPoints = Math.Max(0, events.BonusPoints + amount);
        return events.BonusPoints;
    }

    /// <summary>
    /// Makes a settlement have waited for its event for longer: it has more points of waiting. A negative number makes the wait shorter.
    /// </summary>
    public void AddWaitTime(Entity<SettlementComponent> settlement, TimeSpan time)
    {
        if (_eventsQuery.TryComp(settlement, out var events))
            events.LastEventTime -= time;
    }

    /// <summary>
    /// Makes an event happen to the settlement at once: the chance of it and its delay are not asked.
    /// The points are, unless the event is told.
    /// </summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="proto">The event, one that the narrator can afford is chosen if it is not told.</param>
    /// <returns>The event that has started, null if none has.</returns>
    public EntProtoId? Roll(Entity<SettlementComponent> settlement, EntProtoId? proto = null, bool ignoreDelay = true)
    {
        var narrators = ActiveNarrators();

        if (narrators.Count == 0)
        {
            Log.Warning("There is no active narrator: the event is made of no points of a narrator, and the ones who come with it are not spawned");
            _adminLogger.Add(
                LogType.EventRan,
                LogImpact.Medium,
                $"Narrator: there is no active narrator, so the event {proto} is made of no points and the ones who come with it are not spawned");
        }

        // An event that is told is started whatever the narrator can afford; it is the first narrator's, and it is nobody's if there is
        // none (the event is then made of no points of a narrator).
        if (proto != null)
        {
            return StartEvent(settlement, proto.Value, narrators.Count > 0 ? narrators[0].Owner : null, ignoreDelay)
                ? proto
                : null;
        }

        // Otherwise the event is one that a narrator can afford.
        foreach (var narrator in narrators)
        {
            if (PickRandomEvent(settlement, narrator) is { } chosen
                && StartEvent(settlement, chosen, narrator.Owner, ignoreDelay))
                return chosen;
        }

        return null;
    }
}
