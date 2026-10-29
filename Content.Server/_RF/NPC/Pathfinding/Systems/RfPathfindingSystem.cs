using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared._RF.NPC.Pathfinding.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Pathfinding.Systems;

public sealed partial class RfPathfindingSystem : SharedRfPathfindingSystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    [Dependency] private EntityQuery<DestructibleComponent> _destructibleQuery = default!;

    public override DamageSpecifier? GetDestroyDamage(EntityUid uid)
        => _destructibleQuery.TryComp(uid, out var destructible) ? GetDestroyDamage(destructible) : null;

    public override DamageSpecifier? GetDestroyDamage(EntityPrototype prototype)
        => prototype.TryComp<DestructibleComponent>(out var destructible, _factory)
            ? GetDestroyDamage(destructible)
            : null;

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
                foreach (var type in _prototype.EnumeratePrototypes<DamageTypePrototype>())
                {
                    AddMin(ref result, type.ID, total.Damage);
                }

                break;
            case DamageTypeTrigger type:
                AddMin(ref result, type.DamageType, type.Damage);
                break;
#pragma warning disable CS0618 // DamageGroupPrototype is obsolete, but the trigger uses it
            case DamageGroupTrigger group:
                foreach (var type in _prototype.Index(group.DamageGroup).DamageTypes)
                {
                    AddMin(ref result, type, group.Damage);
                }

                break;
#pragma warning restore CS0618
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
