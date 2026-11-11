using System.Threading.Tasks;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared._RF;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared._RF.NPC.Pathfinding.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Pathfinding.Systems;

public sealed partial class RfPathfindingSystem : SharedRfPathfindingSystem
{
    [Dependency] private IPrototypeManager _proto = null!;
    [Dependency] private EntityQuery<DestructibleComponent> _destructibleQuery;

    public override void Initialize()
    {
        base.Initialize();

        InitializeBaking();
        InitializeRequests();

        // The tag indices have changed, so everything that was built has to be built again.
        Subs.ProtoReload<PathTagPrototype>(_proto, ClearMaps);
        _transform.OnGlobalMoveEvent += OnMoveEvent;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _transform.OnGlobalMoveEvent -= OnMoveEvent;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _jobs.Process();

        var now = _timing.CurTime;
        var options = new ParallelOptions { MaxDegreeOfParallelism = _parallel.ParallelProcessCount };
        var query = AllEntityQuery<RfGridPathfindingComponent, MapGridComponent>();

        while (query.MoveNext(out var uid, out var map, out var grid))
        {
            if (map.DirtyChunks.Count > 0 && now >= map.NextUpdate)
                RebuildDirty(new Entity<RfGridPathfindingComponent, MapGridComponent>(uid, map, grid), options);

            if (now >= map.NextEviction)
                Evict(map, now);
        }
    }

    public override DamageSpecifier? GetDestroyDamage(EntityUid uid)
        => _destructibleQuery.TryComp(uid, out var destructible) ? GetDestroyDamage(destructible) : null;

    // The same instance is returned for every entity of a prototype, so comparing them is cheap.
    public override DamageSpecifier? GetDestroyDamage(EntityPrototype prototype)
        => _destroyDamage.GetOrAdd(prototype.ID,
            _ => prototype.TryComp<DestructibleComponent>(out var destructible, EntityManager.ComponentFactory)
                ? GetDestroyDamage(destructible)
                : null);

    /// <summary>
    /// Converts the thresholds of the entity that destroy or break it to the damage needed for that.
    /// </summary>
    /// <remarks>
    /// The thresholds are alternatives, so the lowest amount of each type is taken.
    /// <see cref="AndTrigger"/> is not supported, because it cannot be expressed this way.
    /// </remarks>
    private DamageSpecifier? GetDestroyDamage(DestructibleComponent destructible)
    {
        DamageSpecifier? result = null;

        foreach (var threshold in destructible.Thresholds)
        {
            foreach (var behavior in threshold.Behaviors)
            {
                if (behavior is not DoActsBehavior act || !act.HasAct(ThresholdActs.Destruction | ThresholdActs.Breakage))
                    continue;

                AddTrigger(ref result, threshold.Trigger);
                break;
            }
        }

        return result;
    }

    private void AddTrigger(ref DamageSpecifier? result, IThresholdTrigger trigger)
    {
        switch (trigger)
        {
            case DamageTrigger total:
                foreach (var type in _proto.EnumeratePrototypes<DamageTypePrototype>())
                {
                    AddMin(ref result, type.ID, total.Damage);
                }

                break;
            case DamageTypeTrigger type:
                AddMin(ref result, type.DamageType, type.Damage);
                break;
            case DamageGroupTrigger group:
                foreach (var type in _proto.Index(group.DamageGroup).DamageTypes)
                {
                    AddMin(ref result, type, group.Damage);
                }

                break;
            case OrTrigger or:
                foreach (var inner in or.Triggers)
                {
                    AddTrigger(ref result, inner);
                }

                break;
        }
    }

    private static void AddMin(ref DamageSpecifier? result, ProtoId<DamageTypePrototype> type, FixedPoint2 amount)
    {
        result ??= new DamageSpecifier();
        result.DamageDict[type] = result.DamageDict.TryGetValue(type, out var current)
            ? FixedPoint2.Min(current, amount)
            : amount;
    }
}
