using Content.Server.CombatMode;
using Content.Server._RF.NPC.GOAP.Actions.Movement;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Random;
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

    /// <summary>
    /// If true, the combat mode is put back as it was before the action when the action is over,
    /// whatever it was (the one that the player has put on is not taken away,
    /// and the one that was not on is not left behind). It is what the action does by default,
    /// and it takes the place of <see cref="RemoveCombatMode"/>.
    /// </summary>
    [DataField]
    public bool RestoreCombatMode = true;

    /// <summary>
    /// If true, combat mode will be disabled when the action is finished. Used when <see cref="RestoreCombatMode"/> is off.
    /// </summary>
    [DataField]
    public bool RemoveCombatMode = true;

    /// <summary>
    /// The key where the combat mode that the agent had when the action began is kept, for <see cref="RestoreCombatMode"/>.
    /// </summary>
    [DataField]
    public StateKey<bool> CombatModeBeforeKey = "MeleeCombatModeBefore";

    /// <summary>
    /// How the agent moves in the fight besides coming up to the target, null if it just stands and hits. Without it the fight is
    /// two bodies that stand opposite each other and trade blows.
    /// </summary>
    [DataField]
    public MeleeFootwork? Footwork;
}

/// <summary>
/// The movement of a melee fighter after a blow: it steps back (and comes up again, the weapon is out of reach for a moment), or
/// steps around the target keeping the distance. What is done is chosen by chance after each blow, so two fighters do not dance in step.
/// </summary>
[DataDefinition]
public sealed partial class MeleeFootwork
{
    /// <summary>
    /// The chance of stepping back after a blow.
    /// </summary>
    [DataField]
    public float StepBackChance = 0.3f;

    /// <summary>
    /// How far back, in meters.
    /// </summary>
    [DataField]
    public float StepBack = 1f;

    /// <summary>
    /// The chance of stepping around the target after a blow, if it did not step back.
    /// </summary>
    [DataField]
    public float StrafeChance = 0.45f;

    /// <summary>
    /// How far along the circle around the target, in meters.
    /// </summary>
    [DataField]
    public float StrafeLength = 1f;

    /// <summary>
    /// The chance that the agent goes around the target to the other side than the last time.
    /// </summary>
    [DataField]
    public float SwitchSideChance = 0.35f;

    /// <summary>
    /// The longest a step takes, in seconds. The step is cut short when it takes longer, such as when the agent was blocked.
    /// </summary>
    [DataField]
    public float StepTime = 0.6f;
}

/// <summary>
/// The state of the footwork of a fighter.
/// </summary>
[RegisterComponent]
public sealed partial class MeleeFootworkComponent : Component
{
    /// <summary>
    /// Until when the step that is made lasts.
    /// </summary>
    public TimeSpan StepUntil;

    /// <summary>
    /// The side the agent goes around the target by, -1 or 1.
    /// </summary>
    public int Side = 1;

    /// <summary>
    /// How many steps the agent has made.
    /// </summary>
    public int Steps;
}

public sealed partial class MeleeActionSystem : GoapActionSystem<Melee>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MoveToActionSystem _moveTo = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private CombatModeSystem _combatMode = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

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

        Set(ent, action.CombatModeBeforeKey, _combatMode.IsInCombatMode(ent));
        _combatMode.SetInCombatMode(ent, true);
        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, Melee action)
    {
        Remove(ent, action.MaxHitsKey);

        if (action.RestoreCombatMode)
        {
            if (TryGet(ent, action.CombatModeBeforeKey, out var before))
                _combatMode.SetInCombatMode(ent, before);

            Remove(ent, action.CombatModeBeforeKey);
        }
        else if (action.RemoveCombatMode)
            _combatMode.SetInCombatMode(ent, false);

        _moveTo.Stop(ent);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, Melee action)
    {
        if (!TryGet(ent, action.TargetKey, out var target))
            return GoapActionResult.Failed;

        if (Deleted(target) || TryComp(target, out MobStateComponent? mobState)
            && mobState.CurrentState > action.TargetState)
            return GoapActionResult.Finished;

        return Attack(ent, target, this, action.MaxHitsKey, action.Footwork);
    }

    private GoapActionResult Attack(
        Entity<GoapComponent> ent,
        EntityUid target,
        GoapDebugDumpSystem handle,
        StateKey<int>? maxHitsKey = null,
        MeleeFootwork? footwork = null)
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

        // A step of the footwork goes on, the agent comes up to the target again when it is over.
        if (footwork != null && _moveTo.IsStepping(ent))
        {
            if (TryComp(ent, out MeleeFootworkComponent? stepping) && stepping.StepUntil > _timing.CurTime)
                return GoapActionResult.Continuing;

            _moveTo.Stop(ent);
        }

        // The target is followed until it is within the reach of the weapon. The reach is counted from the body of
        // the target and not from its center, the center of a big target can be out of reach when it is touched.
        if (distance > weapon.Range)
        {
            var move = _moveTo.Move(ent, handle, target, weapon.Range);

            if (move != GoapActionResult.Finished)
                return move;
        }
        else
        {
            _moveTo.Stop(ent);
        }

        if (weapon.NextAttack > _timing.CurTime)
            return GoapActionResult.Continuing;

        var hit = _melee.AttemptLightAttack(ent, weaponUid, weapon, target);

        if (hit && footwork != null && maxHits != 1)
            Footwork(ent, target, footwork);

        if (hit && maxHits == 1)
            return GoapActionResult.Finished;

        if (maxHitsKey != null)
            Set(ent, maxHitsKey.Value, maxHits - 1);

        return GoapActionResult.Continuing;
    }

    /// <summary>
    /// A step after the blow: back from the target, or around it. If the way is blocked, the agent stays where it is.
    /// </summary>
    private void Footwork(Entity<GoapComponent> ent, EntityUid target, MeleeFootwork footwork)
    {
        var self = _transform.GetWorldPosition(ent);
        var enemy = _transform.GetWorldPosition(target);
        var away = self - enemy;
        var distance = away.Length();

        if (distance < 0.05f)
            return;

        var state = EnsureComp<MeleeFootworkComponent>(ent);
        var roll = _random.NextFloat();
        var started = false;

        if (roll < footwork.StepBackChance)
            started = _moveTo.Step(ent, self + away / distance * footwork.StepBack, target);
        else if (roll < footwork.StepBackChance + footwork.StrafeChance)
        {
            if (_random.Prob(footwork.SwitchSideChance))
                state.Side = -state.Side;

            // Along the circle around the target, the distance to it is the same.
            for (var attempt = 0; attempt < 2 && !started; attempt++)
            {
                var angle = new Angle(footwork.StrafeLength / Math.Max(distance, 0.5f) * state.Side);
                started = _moveTo.Step(ent, enemy + angle.RotateVec(away), target);

                if (!started)
                    state.Side = -state.Side;
            }
        }

        if (started)
            state.Steps++;

        if (started)
            state.StepUntil = _timing.CurTime + TimeSpan.FromSeconds(footwork.StepTime);
    }
}
