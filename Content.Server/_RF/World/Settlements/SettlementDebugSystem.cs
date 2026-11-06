using System.Linq;
using Content.Server._RF.Narrator;
using Content.Server.Administration.Managers;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Phases;
using Content.Shared._RF.World.Components;
using Content.Shared._RF.World.Settlements;
using Content.Shared.Administration;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RF.World.Settlements;

/// <summary>
/// Tells those who debug (see the debug overlay of the settlements on the client) what the settlements are: where, how large, what
/// they own, what it is worth, what the narrator counts for them, and what waits for a settlement with the player.
/// </summary>
public sealed partial class SettlementDebugSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private IPlayerManager _players = null!;
    [Dependency] private IPrototypeManager _prototype = null!;
    [Dependency] private SettlementSystem _settlements = null!;
    [Dependency] private SettlementWealthSystem _wealth = null!;
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private NarratorSystem _narrator = null!;
    [Dependency] private PhasesSystem _phases = null!;

    [Dependency] private EntityQuery<SettlementComponent> _settlementQuery;
    [Dependency] private EntityQuery<SettlementWealthComponent> _wealthQuery;
    [Dependency] private EntityQuery<SettlementEventsComponent> _eventsQuery;
    [Dependency] private EntityQuery<SettlementTrackerComponent> _trackerQuery;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery;
    [Dependency] private EntityQuery<MapComponent> _mapQuery;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery;

    /// <summary>
    /// How often the snapshots are sent.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly HashSet<ICommonSession> _subscribers = new();
    private TimeSpan _nextSend;

    [SubscribeNetworkEvent]
    private void OnRequest(SettlementsDebugRequest msg, EntitySessionEventArgs args)
    {
        if (msg.Enabled && _admin.HasAdminFlag(args.SenderSession, AdminFlags.Debug))
        {
            _subscribers.Add(args.SenderSession);
            _nextSend = TimeSpan.Zero;
        }
        else
        {
            _subscribers.Remove(args.SenderSession);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _subscribers.RemoveWhere(x => x.Status == SessionStatus.Disconnected);

        if (_subscribers.Count == 0 || _timing.CurTime < _nextSend)
            return;

        _nextSend = _timing.CurTime + Interval;

        var msg = Build();

        foreach (var subscriber in _subscribers)
        {
            RaiseNetworkEvent(msg, subscriber);
        }
    }

    private SettlementsDebugMessage Build()
    {
        var now = _timing.CurTime;
        var narrator = _narrator.ActiveNarrators().FirstOrDefault();
        var infos = new List<SettlementDebugInfo>();
        var waiting = new List<SettlementDebugEntity>();

        foreach (var settlement in _settlements.AllSettlements(true))
        {
            var comp = settlement.Comp;
            _wealthQuery.TryComp(settlement, out var wealth);
            _eventsQuery.TryComp(settlement, out var events);

            var members = new List<SettlementDebugEntity>();
            var owned = _ownership.GetOwned(settlement.Owner);

            foreach (var uid in owned)
            {
                if (Member(uid, wealth, settlement, now) is { } member)
                    members.Add(member);
            }

            var eventPoints = -1;
            var waitPoints = 0;

            if (narrator.Comp != null && events != null && _prototype.TryIndex(narrator.Comp.Narrator, out var proto))
            {
                eventPoints = _narrator.EventPoints(settlement, narrator.Comp.Narrator);
                waitPoints = _narrator.WaitPoint(proto, events);
            }

            var reach = _trackerQuery.TryComp(comp.Player, out var tracker) ? tracker.Radius : 0f;

            infos.Add(new SettlementDebugInfo(
                GetNetEntity(settlement),
                GetNetEntity(comp.Player),
                _players.TryGetSessionByEntity(comp.Player, out var session) ? session.Name : string.Empty,
                GetNetCoordinates(_settlements.GetCoordinates(settlement)),
                comp.Radius,
                reach,
                comp.ResidentCount,
                owned.Count,
                comp.Dissolving,
                comp.DissolveAt is { } at ? (float) Math.Max(0, (at - now).TotalSeconds) : -1f,
                wealth == null ? new Dictionary<string, double>() : new Dictionary<string, double>(wealth.Values),
                eventPoints,
                waitPoints,
                events?.BonusPoints ?? 0,
                members));
        }

        // What waits with the players: what they own, and the settlements are not that.
        var players = EntityQueryEnumerator<RimFortressPlayerComponent>();

        while (players.MoveNext(out var player, out _))
        {
            foreach (var uid in _ownership.GetOwned(player))
            {
                if (_settlementQuery.HasComp(uid))
                    continue;

                if (Member(uid, null, null, now) is { } member)
                    waiting.Add(member);
            }
        }

        return new SettlementsDebugMessage(infos, waiting, BuildEvents(now));
    }

    private List<EventDebugInfo> BuildEvents(TimeSpan now)
    {
        var events = new List<EventDebugInfo>();
        var query = EntityQueryEnumerator<WorldRuleComponent, ActiveGameRuleComponent>();

        while (query.MoveNext(out var uid, out var rule, out _))
        {
            var entities = new List<SettlementDebugEntity>();

            // The ones who have come: the living owned by the event.
            foreach (var owned in _ownership.GetOwned(uid))
            {
                if (_mobStateQuery.HasComp(owned) && Member(owned, null, null, now) is { } member)
                    entities.Add(member);
            }

            var narrator = rule.Narrator is { } n && TryComp(n, out NarratorRuleComponent? narratorRule)
                ? narratorRule.Narrator.Id
                : "none";

            events.Add(new EventDebugInfo(
                GetNetEntity(uid),
                MetaData(uid).EntityPrototype?.ID ?? ToPrettyString(uid),
                GetNetEntity(rule.Settlement),
                GetNetCoordinates(TerminatingOrDeleted(rule.Settlement) ? rule.TargetCoordinates : _settlements.GetCoordinates(rule.Settlement)),
                _phases.GetPhase(uid) ?? "active",
                narrator,
                entities));
        }

        return events;
    }

    /// <summary>
    /// An owned entity as the overlay shows it: only what lies on the ground of the world, what the pops carry is shown with the pops.
    /// </summary>
    private SettlementDebugEntity? Member(
        EntityUid uid,
        SettlementWealthComponent? wealth,
        Entity<SettlementComponent>? settlement,
        TimeSpan now)
    {
        if (TerminatingOrDeleted(uid) || !_xformQuery.TryComp(uid, out var xform) || !_gridQuery.HasComp(xform.ParentUid) && !_mapQuery.HasComp(xform.ParentUid))
            return null;

        var category = wealth != null ? _wealth.Categorize(wealth, uid) : null;
        var away = -1f;

        if (settlement is { } owner && _settlements.TryGetAwaySince(owner, uid, out var since))
            away = (float) (now - since).TotalSeconds;

        return new SettlementDebugEntity(GetNetEntity(uid), category ?? "Other", away);
    }
}
