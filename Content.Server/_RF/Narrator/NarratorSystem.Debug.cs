using System.Linq;
using System.Text;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World.Settlements;
using Content.Shared.GameTicking.Components;
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
    /// Makes an event happen to the settlement at once: the chance of it and its delay are not asked. The points are, unless the event
    /// is told.
    /// </summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="proto">The event, one that the narrator can afford is chosen if it is not told.</param>
    /// <returns>The event that has started, null if none has.</returns>
    public EntProtoId? Roll(Entity<SettlementComponent> settlement, EntProtoId? proto = null)
    {
        var narrators = ActiveNarrators();

        // An event that is told is started whatever the narrator can afford; it is the first narrator's, and it is nobody's if there is
        // none (the event is then made of no points of a narrator).
        if (proto != null)
            return StartEvent(settlement, proto.Value, narrators.Count > 0 ? narrators[0].Owner : null, true) ? proto : null;

        // Otherwise the event is one that a narrator can afford.
        foreach (var narrator in narrators)
        {
            if (PickRandomEvent(settlement, narrator) is { } chosen && StartEvent(settlement, chosen, narrator.Owner, true))
                return chosen;
        }

        return null;
    }
}
