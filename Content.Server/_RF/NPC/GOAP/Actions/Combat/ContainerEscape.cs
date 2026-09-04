using Content.Server.CombatMode;
using Content.Server.NPC.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Server.Containers;

namespace Content.Server._RF.NPC.GOAP.Actions.Combat;

/// <summary>
/// The agent will try to escape from the container by opening or breaking it.
/// </summary>
public sealed partial class ContainerEscape : BaseGoapAction<ContainerEscape>
{
    /// <summary>
    /// A key that stores the container entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed partial class EscapeSystem : GoapActionSystem<ContainerEscape>
{
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private EntityStorageSystem _entityStorage = default!;
    [Dependency] private CombatModeSystem _combatMode = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, ContainerEscape action)
    {
        if (!TryGet(ent, action.TargetKey, out var target))
            return false;

        if (_entityStorage.TryOpenStorage(ent, target))
            return true;

        EnsureComp<NPCMeleeCombatComponent>(ent).Target = target;
        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, ContainerEscape action)
    {
        _combatMode.SetInCombatMode(ent, false);
        RemComp<NPCMeleeCombatComponent>(ent);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, ContainerEscape action)
    {
        if (!_container.IsEntityInContainer(ent))
            return GoapActionResult.Finished;

        if (!TryGet(ent, action.TargetKey, out _))
            return GoapActionResult.Failed;

        if (!TryComp(ent, out NPCMeleeCombatComponent? comp))
        {
            ComponentNotFound<NPCMeleeCombatComponent>();
            return GoapActionResult.Failed;
        }

        switch (comp.Status)
        {
            case CombatStatus.TargetOutOfRange:
            case CombatStatus.Normal:
                return GoapActionResult.Continuing;
            default:
                CreateDump($"NPCMeleeCombat returned status '{comp.Status}'");
                return GoapActionResult.Failed;
        }
    }
}
