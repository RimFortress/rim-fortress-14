using Content.Shared._RF.Phases;
using Content.Server._RF.World;
using Content.Server._RF.World.Settlements;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manages <see cref="RimFortressRuleComponent"/>
/// </summary>
public sealed partial class RimFortressRuleSystem : GameRuleSystem<RimFortressRuleComponent>
{
    [Dependency] private RimFortressWorldSystem _world = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SettlementSystem _settlements = default!;

    protected override void Added(EntityUid uid, RimFortressRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        _world.InitializeWorld(uid, comp);
    }

    [SubscribeLocalEvent]
    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        var query = EntityQueryEnumerator<RimFortressRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            _world.SpawnPlayer(ev.Player);

            ev.Handled = true;
            return;
        }
    }

    [SubscribeLocalEvent]
    private void OnPhaseChanged(ref PhaseChangedEvent args)
    {
        if (HasComp<WorldRuleComponent>(args.Entity))
            _adminLogger.Add(LogType.EventRan, LogImpact.Low, $"World rule {ToPrettyString(args.Entity)}: phase {args.From ?? "-"} -> {args.To}");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DelayedStartRuleComponent, WorldRuleComponent>();
        while (query.MoveNext(out var uid, out var delay, out var rule))
        {
            if (_timing.CurTime < delay.RuleStartTime)
                continue;

            StartWorldRule(new(uid, rule));
        }
    }

    public RimFortressRuleComponent? GetRule()
    {
        while (EntityQueryEnumerator<RimFortressRuleComponent>().MoveNext(out var comp))
        {
            return comp;
        }

        return null;
    }

    public bool IsGameRuleActive(EntityUid ruleEntity, WorldRuleComponent? component = null)
    {
        return Resolve(ruleEntity, ref component) && HasComp<ActiveGameRuleComponent>(ruleEntity);
    }

    /// <summary>
    /// Adds a world rule for a settlement to the list, but does not
    /// start it yet, instead waiting until the rule is actually started by other code.
    /// The events depend on the settlement alone: the player that the settlement belongs to is reached through it.
    /// </summary>
    /// <returns>The entity for the added worldrule</returns>
    [PublicAPI]
    public EntityUid AddWorldRule(EntProtoId ruleId, EntityUid settlement)
    {
        var coordinates = _settlements.GetCoordinates(settlement);
        var ruleEntity = Spawn(ruleId, MapCoordinates.Nullspace);
        var comp = Comp<WorldRuleComponent>(ruleEntity);

        comp.Settlement = settlement;
        comp.TargetCoordinates = coordinates;

        var str = $"Added world rule {ToPrettyString(ruleEntity)} for {ToPrettyString(settlement)}";
        Log.Info(str);
        _chat.SendAdminAnnouncement(str);

        _adminLogger.Add(LogType.EventStarted, $"Added game rule {ToPrettyString(ruleEntity)} for {ToPrettyString(settlement)}");

        var ev = new WorldRuleAddedEvent(ruleEntity, ruleId, settlement, coordinates);
        RaiseLocalEvent(ruleEntity, ref ev, true);
        return ruleEntity;
    }

    /// <summary>
    /// Adds a world rule that is about a settlement and starts it. The coordinates of the target are the center of the
    /// settlement.
    /// </summary>
    [PublicAPI]
    public bool StartWorldRule(EntProtoId ruleId, EntityUid settlement, bool ignoreDelay = false)
    {
        return StartWorldRule(AddWorldRule(ruleId, settlement), ignoreDelay);
    }

    [PublicAPI]
    public bool StartWorldRule(Entity<WorldRuleComponent?> ruleEntity, bool ignoreDelay = false)
    {
        if (!Resolve(ruleEntity, ref ruleEntity.Comp)
            || !ruleEntity.Comp.Settlement.IsValid()
            || TerminatingOrDeleted(ruleEntity.Comp.Settlement)
            || HasComp<ActiveGameRuleComponent>(ruleEntity)
            || HasComp<EndedGameRuleComponent>(ruleEntity)
            || MetaData(ruleEntity).EntityPrototype is not { } proto)
            return false;

        // The settlement can have moved while the rule waited.
        var settlement = ruleEntity.Comp.Settlement;
        var targetCoordinates = _settlements.GetCoordinates(settlement);
        ruleEntity.Comp.TargetCoordinates = targetCoordinates;

        // If we already have it, then we just skip the delay as it has already happened.
        if (!ignoreDelay && !RemComp<DelayedStartRuleComponent>(ruleEntity) && ruleEntity.Comp.Delay is { } delay)
        {
            var delayTime = TimeSpan.FromSeconds(delay.Next(_random));

            if (delayTime > TimeSpan.Zero)
            {
                var str = $"Queued start for world rule {ToPrettyString(ruleEntity)} with delay {delayTime}";
                Log.Info(str);
                _chat.SendAdminAnnouncement(str);
                _adminLogger.Add(LogType.EventStarted,
                    $"Queued start for world rule {ToPrettyString(ruleEntity)} with delay {delayTime}");

                var delayed = EnsureComp<DelayedStartRuleComponent>(ruleEntity);
                delayed.RuleStartTime = _timing.CurTime + delayTime;
                return true;
            }
        }

        var msg = $"Started world rule {ToPrettyString(ruleEntity)}";
        Log.Info(msg);
        _chat.SendAdminAnnouncement(msg);
        _adminLogger.Add(LogType.EventStarted,
            $"Started world rule {ToPrettyString(ruleEntity)}");

        EnsureComp<ActiveGameRuleComponent>(ruleEntity);

        var ev = new WorldRuleStartedEvent(ruleEntity, proto, settlement, targetCoordinates);
        RaiseLocalEvent(ruleEntity, ref ev, true);
        return true;
    }

    /// <summary>
    /// Ends a world rule.
    /// </summary>
    /// <param name="uid">The rule.</param>
    /// <param name="result">How it ends, if the one who ends it says.</param>
    [PublicAPI]
    public bool EndWorldRule(Entity<WorldRuleComponent?> uid, string? result = null)
    {
        if (!Resolve(uid, ref uid.Comp))
            return false;

        // don't end it multiple times
        if (HasComp<EndedGameRuleComponent>(uid))
            return false;

        if (MetaData(uid).EntityPrototype is not { } proto) // you really fucked up
            return false;

        uid.Comp.Result ??= result;

        RemComp<ActiveGameRuleComponent>(uid);
        EnsureComp<EndedGameRuleComponent>(uid);

        var ended = $"Ended world rule {ToPrettyString(uid)} for {ToPrettyString(uid.Comp.Settlement)}: {uid.Comp.Result ?? "no result"}";
        Log.Info(ended);
        _chat.SendAdminAnnouncement(ended);
        _adminLogger.Add(LogType.EventStopped, $"Ended world rule {ToPrettyString(uid)} for {ToPrettyString(uid.Comp.Settlement)}");

        var ev = new WorldRuleEndedEvent(uid, proto, uid.Comp.Settlement, uid.Comp.TargetCoordinates, uid.Comp.Result);
        RaiseLocalEvent(uid, ref ev, true);
        return true;
    }
}

/// <summary>
/// Raised when adding a world event
/// </summary>
/// <param name="RuleEntity">Entity of the world event</param>
/// <param name="RuleId">Prototype of the world event</param>
/// <param name="Settlement">The settlement that the event is about</param>
/// <param name="TargetCoordinates">Target coordinates of the event</param>
[ByRefEvent, PublicAPI]
public readonly record struct WorldRuleAddedEvent(
    EntityUid RuleEntity,
    EntProtoId RuleId,
    EntityUid Settlement,
    EntityCoordinates TargetCoordinates);

/// <summary>
/// Raised when the world event starts
/// </summary>
/// <param name="RuleEntity">Entity of the world event</param>
/// <param name="RuleId">Prototype of the world event</param>
/// <param name="Settlement">The settlement that the event is about</param>
/// <param name="TargetCoordinates">Target coordinates of the event</param>
[ByRefEvent, PublicAPI]
public readonly record struct WorldRuleStartedEvent(
    EntityUid RuleEntity,
    EntProtoId RuleId,
    EntityUid Settlement,
    EntityCoordinates TargetCoordinates);

/// <summary>
/// Raised when the world event is ended
/// </summary>
/// <param name="RuleEntity">Entity of the world event</param>
/// <param name="RuleId">Prototype of the world event</param>
/// <param name="Settlement">The settlement that the event is about</param>
/// <param name="TargetCoordinates">Target coordinates of the event</param>
/// <param name="Result">How the event has ended, if the one who ended it said</param>
[ByRefEvent, PublicAPI]
public readonly record struct WorldRuleEndedEvent(
    EntityUid RuleEntity,
    EntProtoId RuleId,
    EntityUid Settlement,
    EntityCoordinates TargetCoordinates,
    string? Result = null);
