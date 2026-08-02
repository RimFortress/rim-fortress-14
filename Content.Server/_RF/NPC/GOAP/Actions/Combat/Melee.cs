using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.CombatMode;
using Content.Server.NPC.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

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
}

public sealed class MeleeActionSystem : GoapActionSystem<Melee>
{
    [Dependency] private readonly CombatModeSystem _combatMode = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Melee action)
    {
        if (!TryGetValue(ent, action, action.TargetKey, out var target))
            return false;

        if (TryComp(target, out MobStateComponent? mobState)
            && mobState.CurrentState > action.TargetState)
        {
            CreateDump(ent,
                action,
                $"target.CurrentState: {mobState.CurrentState} > action.TargetState: {action.TargetState}");
            return false;
        }

        var melee = EnsureComp<NPCMeleeCombatComponent>(ent);
        melee.Target = target;
        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, Melee action)
    {
        _combatMode.SetInCombatMode(ent, false);
        RemComp<NPCMeleeCombatComponent>(ent);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, Melee action)
    {
        if (!TryComp(ent, out NPCMeleeCombatComponent? melee))
        {
            ComponentNotFound<NPCMeleeCombatComponent>(ent, action);
            return GoapActionResult.Failed;
        }

        if (!TryGetValue(ent, action, action.TargetKey, out var target))
            return GoapActionResult.Failed;

        if (Deleted(target) || TryComp(target, out MobStateComponent? mobState)
            && mobState.CurrentState > action.TargetState)
            return GoapActionResult.Finished;

        switch (melee.Status)
        {
            case CombatStatus.TargetOutOfRange:
            case CombatStatus.Normal:
                return GoapActionResult.Continuing;
            default:
                CreateDump(ent, action, $"NPCMeleeCombat returned status `{melee.Status}`");
                return GoapActionResult.Failed;
        }
    }
}
