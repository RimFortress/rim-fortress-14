using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.NPC.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.CombatMode;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;

namespace Content.Server._RF.NPC.GOAP.Actions.Combat;

/// <summary>
/// Action responsible for the logic of the agent's firing at the target entity.
/// </summary>
public sealed partial class Gun : BaseGoapAction<Gun>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    /// <summary>
    /// Minimum damage state that the target has to be in for us to consider attacking.
    /// </summary>
    [DataField]
    public MobState TargetState = MobState.Alive;

    /// <summary>
    /// Do we require line of sight of the target before failing?
    /// </summary>
    [DataField]
    public bool RequireLos;

    /// <summary>
    /// If true, only opaque objects will block line of sight.
    /// </summary>
    [DataField]
    public bool UseOpaqueForLosChecks;

    [ViewVariables]
    public static readonly StateKey<SoundSpecifier> SoundTargetInLos = "SoundTargetInLos";
}

public sealed class GunActionSystem : GoapActionSystem<Gun>
{
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly EntityQuery<MobStateComponent> _mobStateQuery = default!;
    [Dependency] private readonly EntityQuery<NPCRangedCombatComponent> _combatQuery = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Gun action)
    {
        if (!TryGetValue(ent, action, action.TargetKey, out var target))
            return false;

        if (_mobStateQuery.TryComp(target, out var mobState)
            && mobState.CurrentState > action.TargetState)
            return true;

        var ranged = EnsureComp<NPCRangedCombatComponent>(ent);
        ranged.Target = target;
        ranged.UseOpaqueForLOSChecks = action.UseOpaqueForLosChecks;

        if (TryGetValue(ent, action, GoapState.RotateSpeed, out var rotateSpeed))
            ranged.RotationSpeed = new Angle(rotateSpeed);

        if (TryGetValue(ent, action, Gun.SoundTargetInLos, out var sound))
            ranged.SoundTargetInLOS = sound;

        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, Gun action)
    {
        _combatMode.SetInCombatMode(ent, false);
        RemComp<NPCRangedCombatComponent>(ent);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, Gun action)
    {
        if (!TryGetValue(ent, action, action.TargetKey, out var target))
            return GoapActionResult.Failed;

        // Success
        if (Deleted(target)
            || _mobStateQuery.TryComp(target, out var mobState)
            && mobState.CurrentState > action.TargetState)
            return GoapActionResult.Finished;

        if (!_combatQuery.TryComp(ent, out var combat))
        {
            ComponentNotFound<NPCRangedCombatComponent>(ent, action);
            return GoapActionResult.Failed;
        }

        combat.Target = target;

        if (combat.Status != CombatStatus.Normal)
            CreateDump(ent, action, $"NPCRangedCombat returned status: `{combat.Status}`");

        return combat.Status switch
        {
            CombatStatus.NotInSight => action.RequireLos
                ? GoapActionResult.Failed
                : GoapActionResult.Continuing,
            CombatStatus.Normal => GoapActionResult.Continuing,
            _ => GoapActionResult.Failed,
        };
    }
}
