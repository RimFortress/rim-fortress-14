using System.Numerics;
using Content.Server.CombatMode;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.GOAP.Actions.Combat;

/// <summary>
/// Attacks the specified key in melee combat.
/// </summary>
public sealed partial class Melee : BaseGoapAction<Melee>
{
    /// <summary>
    /// Key that contains the target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    /// <summary>
    /// Minimum damage state that the target has to be in for us to consider attacking.
    /// </summary>
    [DataField]
    public MobState TargetState = MobState.Alive;

    /// <summary>
    /// The maximum number of hits that can be dealt; -1 means there is no limit.
    /// </summary>
    [DataField]
    public int MaxHits = -1;

    /// <summary>
    /// A key that will store the remaining number of hits, if there is a limit.
    /// </summary>
    [DataField]
    public StateKey<int> MaxHitsKey = "MeleeMaxHits";
}

public sealed class MeleeActionSystem : GoapActionSystem<Melee>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly CombatModeSystem _combatMode = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Melee action)
    {
        if (!TryGet(ent, action.TargetKey, out var target))
            return false;

        Set(ent, action.MaxHitsKey, action.MaxHits);

        if (TryComp(target, out MobStateComponent? mobState)
            && mobState.CurrentState > action.TargetState)
        {
            CreateDump($"target.CurrentState: {mobState.CurrentState} > action.TargetState: {action.TargetState}");
            return false;
        }

        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, Melee action)
    {
        Remove(ent, action.MaxHitsKey);
        _combatMode.SetInCombatMode(ent, false);
        _steering.Unregister(ent);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, Melee action)
    {
        if (!TryGet(ent, action.TargetKey, out var target))
            return GoapActionResult.Failed;

        if (Deleted(target) || TryComp(target, out MobStateComponent? mobState)
            && mobState.CurrentState > action.TargetState)
            return GoapActionResult.Finished;

        return Attack(ent, target, this, action.MaxHitsKey);
    }

    private GoapActionResult Attack(
        Entity<GoapComponent> ent,
        EntityUid target,
        GoapDebugDumpSystem handle,
        StateKey<int>? maxHitsKey = null)
    {
        var maxHits = -1;

        if (maxHitsKey != null && !handle.TryGet(ent, maxHitsKey.Value, out maxHits))
            return GoapActionResult.Failed;

        if (!_melee.TryGetWeapon(ent, out var weaponUid, out var weapon))
        {
            handle.CreateDump($"melee weapon not found");
            return GoapActionResult.Failed;
        }

        if (!EntityManager.TransformQuery.TryComp(target, out var targetXform))
        {
            handle.ComponentNotFound<TransformComponent>(target);
            return GoapActionResult.Failed;
        }

        if (!Transform(ent).Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance)
            || distance > Get(ent, GoapState.TargetMeleeLostRange))
        {
            handle.CreateDump($"target unreachable");
            return GoapActionResult.Failed;
        }

        if (TryComp<NPCSteeringComponent>(ent, out var steering) &&
            steering.Status == SteeringStatus.NoPath)
        {
            handle.CreateDump("steering return NoPath");
            return GoapActionResult.Failed;
        }

        _steering.Register(ent, new EntityCoordinates(target, Vector2.Zero), steering);

        if (distance > weapon.Range
            || weapon.NextAttack > _timing.CurTime)
            return GoapActionResult.Continuing;

        if (_melee.AttemptLightAttack(ent, weaponUid, weapon, target) && maxHits == 1)
            return GoapActionResult.Finished;

        if (maxHitsKey != null)
            Set(ent, maxHitsKey.Value, maxHits - 1);

        return GoapActionResult.Continuing;
    }
}
