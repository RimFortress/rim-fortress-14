using System.Linq;
using Content.Server._RF.World;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Server.Parallax;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World;
using Content.Shared.Database;
using Content.Shared.EntityTable;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random.Helpers;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manages <see cref="RimFortressRuleComponent"/>
/// </summary>
public sealed partial class RimFortressRuleSystem : GameRuleSystem<RimFortressRuleComponent>
{
    [Dependency] private readonly RimFortressWorldSystem _world = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityTableSystem _table = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IConsoleHost _host = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private ISawmill _sawmill = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        _sawmill = LogManager.GetSawmill("rf_rule");

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);

        InitializeCommands();
    }

    protected override void Added(EntityUid uid, RimFortressRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        _world.InitializeWorld(comp);
        comp.NextEventTime = _timing.CurTime + TimeSpan.FromSeconds(comp.MinMaxEventTiming.Next(_random));
    }

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

    private List<(EntityCoordinates Coords, EntProtoId Proto, WorldRuleComponent Comp)> AvailableRules(Entity<RimFortressPlayerComponent> uid)
    {
        var available = new List<(EntityCoordinates, EntProtoId, WorldRuleComponent)>();

        var query = EntityQueryEnumerator<RimFortressRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var rf, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid, rule))
                continue;

            foreach (var spawn in _table.GetSpawns(rf.WorldEvents))
            {
                var proto = _prototype.Index(spawn);

                if (!proto.TryGetComponent(out WorldRuleComponent? worldRule, EntityManager.ComponentFactory)
                    || GameTicker.RoundDuration() < worldRule.StartOffset)
                    continue;

                var coords = _world.GetPlayerSettlements(new(uid, uid.Comp));
                foreach (var coord in coords)
                {
                    if (_transform.GetGrid(coord) is not { } gridUid)
                        continue;

                    var indicates = _map.TileIndicesFor(gridUid, Comp<MapGridComponent>(gridUid), coord);

                    if (worldRule.RequiredBiomes != null
                        && (!_biome.TryGetBiome(indicates, gridUid, out var biome)
                            || !worldRule.RequiredBiomes.Contains(biome)))
                        continue;

                    available.Add((coord, spawn, worldRule));
                }
            }
        }

        return available;
    }

    private (EntityCoordinates Coords, EntProtoId Proto, WorldRuleComponent Comp)? PickRandomRules(
        Entity<RimFortressPlayerComponent> uid)
    {
        var threshold = _random.NextFloat(-1, 1);
        var rules = AvailableRules(uid)
            .Where(x => x.Comp.Threshold <= threshold)
            .ToList();

        if (rules.Count == 0)
            return null;

        return _random.Pick(rules);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var entities = EntityQueryEnumerator<RimFortressPlayerComponent>();
        while (entities.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextEventTime || HasDelayedEvent(new(uid, comp)))
                continue;

            comp.NextEventTime = _timing.CurTime + comp.EventTimeOffset;

            if (PickRandomRules(new(uid, comp)) is not { } rule)
                continue;

            StartWorldRule(rule.Proto, uid, rule.Coords);
        }

        var query = EntityQueryEnumerator<DelayedStartRuleComponent, WorldRuleComponent>();
        while (query.MoveNext(out var uid, out var delay, out var rule))
        {
            if (_timing.CurTime < delay.RuleStartTime)
                continue;

            StartWorldRule(new(uid, rule));
        }

        var rules = EntityQueryEnumerator<RimFortressRuleComponent>();
        while (rules.MoveNext(out var comp))
        {
            if (!_prototype.TryIndex(comp.GlobalEvents, out var proto) || comp.NextEventTime < _timing.CurTime)
                continue;

            comp.NextEventTime = _timing.CurTime + TimeSpan.FromSeconds(comp.MinMaxEventTiming.Next(_random));
            GameTicker.StartGameRule(proto.Pick(_random));
        }
    }

    public bool IsGameRuleActive(EntityUid ruleEntity, WorldRuleComponent? component = null)
    {
        return Resolve(ruleEntity, ref component) && HasComp<ActiveGameRuleComponent>(ruleEntity);
    }

    public bool HasDelayedEvent(Entity<RimFortressPlayerComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return false;

        var query = EntityQueryEnumerator<DelayedStartRuleComponent, WorldRuleComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.Target == entity.Owner)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds a world rule to the list, but does not
    /// start it yet, instead waiting until the rule is actually started by other code
    /// </summary>
    /// <returns>The entity for the added worldrule</returns>
    [PublicAPI]
    public EntityUid AddWorldRule(EntProtoId ruleId, EntityUid target, EntityCoordinates targetCoordinates)
    {
        var ruleEntity = Spawn(ruleId, MapCoordinates.Nullspace);
        var comp = Comp<WorldRuleComponent>(ruleEntity);

        comp.Target = target;
        comp.TargetCoordinates = targetCoordinates;

        var str = $"Added world rule {ToPrettyString(ruleEntity)} for {ToPrettyString(target)}";
        _sawmill.Info(str);
        _chat.SendAdminAnnouncement(str);

        _adminLogger.Add(LogType.EventStarted, $"Added game rule {ToPrettyString(ruleEntity)} for {ToPrettyString(target)}");

        var ev = new WorldRuleAddedEvent(ruleEntity, ruleId, target, targetCoordinates);
        RaiseLocalEvent(ruleEntity, ref ev, true);
        return ruleEntity;
    }

    /// <summary>
    /// World rules can be 'started' separately from being added. 'Starting' them usually
    /// happens at round start while they can be added and removed before then.
    /// </summary>
    [PublicAPI]
    public bool StartWorldRule(
        EntProtoId ruleId,
        EntityUid target,
        EntityCoordinates targetCoordinates,
        bool ignoreDelay = false)
    {
        return StartWorldRule(ruleId, target, targetCoordinates, out _, ignoreDelay);
    }

    /// <summary>
    /// World rules can be 'started' separately from being added. 'Starting' them usually
    /// happens at round start while they can be added and removed before then.
    /// </summary>
    [PublicAPI]
    public bool StartWorldRule(
        EntProtoId ruleId,
        EntityUid target,
        EntityCoordinates targetCoordinates,
        out EntityUid ruleEntity,
        bool ignoreDelay = false)
    {
        ruleEntity = AddWorldRule(ruleId, target, targetCoordinates);
        return StartWorldRule(ruleEntity, ignoreDelay);
    }

    [PublicAPI]
    public bool StartWorldRule(Entity<WorldRuleComponent?> ruleEntity, bool ignoreDelay = false)
    {
        if (!Resolve(ruleEntity, ref ruleEntity.Comp)
            || !ruleEntity.Comp.Target.IsValid()
            || !ruleEntity.Comp.TargetCoordinates.IsValid(EntityManager))
            return false;

        return StartWorldRule(ruleEntity, ruleEntity.Comp.Target, ruleEntity.Comp.TargetCoordinates, ignoreDelay);
    }

    /// <summary>
    /// Game rules can be 'started' separately from being added. 'Starting' them usually
    /// happens at round start while they can be added and removed before then.
    /// </summary>
    [PublicAPI]
    public bool StartWorldRule(
        Entity<WorldRuleComponent?> ruleEntity,
        EntityUid target,
        EntityCoordinates targetCoordinates,
        bool ignoreDelay = false)
    {
        if (!Resolve(ruleEntity, ref ruleEntity.Comp)
            || HasComp<ActiveGameRuleComponent>(ruleEntity)
            || HasComp<EndedGameRuleComponent>(ruleEntity)
            || MetaData(ruleEntity).EntityPrototype is not { } proto)
            return false;

        ruleEntity.Comp.TargetCoordinates = targetCoordinates;
        ruleEntity.Comp.Target = target;

        // If we already have it, then we just skip the delay as it has already happened.
        if (!ignoreDelay && !RemComp<DelayedStartRuleComponent>(ruleEntity) && ruleEntity.Comp.Delay is { } delay)
        {
            var delayTime = TimeSpan.FromSeconds(delay.Next(_random));

            if (delayTime > TimeSpan.Zero)
            {
                var str = $"Queued start for world rule {ToPrettyString(ruleEntity)} " +
                          $"with delay {delayTime} for {ToPrettyString(ruleEntity)}";
                _sawmill.Info(str);
                _chat.SendAdminAnnouncement(str);
                _adminLogger.Add(LogType.EventStarted,
                    $"Queued start for world rule {ToPrettyString(ruleEntity)} " +
                    $"with delay {delayTime} for {ToPrettyString(ruleEntity)}");

                var delayed = EnsureComp<DelayedStartRuleComponent>(ruleEntity);
                delayed.RuleStartTime = _timing.CurTime + delayTime;
                return true;
            }
        }

        var msg = $"Started world rule {ToPrettyString(ruleEntity)} for {ToPrettyString(ruleEntity)}";
        _sawmill.Info(msg);
        _chat.SendAdminAnnouncement(msg);
        _adminLogger.Add(LogType.EventStarted,
            $"Started world rule {ToPrettyString(ruleEntity)} for {ToPrettyString(ruleEntity)}");

        EnsureComp<ActiveGameRuleComponent>(ruleEntity);

        var ev = new WorldRuleStartedEvent(ruleEntity, proto, target, targetCoordinates);
        RaiseLocalEvent(ruleEntity, ref ev, true);
        return true;
    }

    /// <summary>
    /// Ends a world rule.
    /// </summary>
    [PublicAPI]
    public bool EndWorldRule(Entity<WorldRuleComponent?> uid)
    {
        if (!Resolve(uid, ref uid.Comp))
            return false;

        // don't end it multiple times
        if (HasComp<EndedGameRuleComponent>(uid))
            return false;

        if (MetaData(uid).EntityPrototype is not { } proto) // you really fucked up
            return false;

        RemComp<ActiveGameRuleComponent>(uid);
        EnsureComp<EndedGameRuleComponent>(uid);

        _sawmill.Info($"Ended world rule {ToPrettyString(uid)} for {ToPrettyString(uid.Comp.Target)}");
        _adminLogger.Add(LogType.EventStopped, $"Ended world rule {ToPrettyString(uid)} for {ToPrettyString(uid.Comp.Target)}");

        var ev = new WorldRuleEndedEvent(uid, proto, uid.Comp.Target, uid.Comp.TargetCoordinates);
        RaiseLocalEvent(uid, ref ev, true);
        return true;
    }
}

[ByRefEvent]
public readonly record struct WorldRuleAddedEvent(EntityUid RuleEntity, EntProtoId RuleId, EntityUid Target, EntityCoordinates TargetCoordinates);

[ByRefEvent]
public readonly record struct WorldRuleStartedEvent(EntityUid RuleEntity, EntProtoId RuleId, EntityUid Target, EntityCoordinates TargetCoordinates);

[ByRefEvent]
public readonly record struct WorldRuleEndedEvent(EntityUid RuleEntity, EntProtoId RuleId, EntityUid Target, EntityCoordinates TargetCoordinates);
