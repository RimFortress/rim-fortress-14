using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server._RF.NPC.GOAP.Actions.Combat;

/// <summary>
/// Changes bolt state of target weapon.
/// </summary>
public sealed partial class SwitchBoltClosed : BaseGoapAction<SwitchBoltClosed>
{
    /// <summary>
    /// A key containing the target weapon entity.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = GoapState.ActiveHandEntity;

    /// <summary>
    /// Bolt target state. If null, the state will be switched to the opposite one.
    /// </summary>
    [DataField]
    public bool? TargetState;

    /// <summary>
    /// Will the action fail if the target entity does not have bolt?
    /// </summary>
    [DataField]
    public bool FailIfNoChamber;
}

public sealed class SwitchBoltClosedActionSystem : GoapActionSystem<SwitchBoltClosed>
{
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly EntityQuery<ChamberMagazineAmmoProviderComponent> _chamberQuery = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, SwitchBoltClosed action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, SwitchBoltClosed action)
    {
        if (!TryGetValue(ent, action, action.TargetKey, out var target))
            return false;

        if (!_chamberQuery.TryComp(target, out var comp))
        {
            ComponentNotFound<ChamberMagazineAmmoProviderComponent>(ent, action);
            return false;
        }

        if (comp.BoltClosed == null)
        {
            CreateDump(ent, action, $"{ToPrettyString(target)} has no bolt");
            return !action.FailIfNoChamber;
        }

        if (comp.BoltClosed == action.TargetState)
            return true;

        if (comp.BoltClosed == true && action.TargetState != true)
        {
            _gun.SetBoltClosed(target, comp, false, ent);
            return true;
        }

        if (comp.BoltClosed == false && action.TargetState != false)
        {
            _gun.SetBoltClosed(target, comp, true, ent);
            return true;
        }

        return false;
    }
}
