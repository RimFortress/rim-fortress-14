using Content.Server.Wieldable;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Wieldable.Components;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Switches target entity wield.
/// </summary>
public sealed partial class Wield : BaseGoapAction<Wield>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = GoapState.ActiveHandEntity;

    /// <summary>
    /// Target wield state. If null, the state will be switched to the opposite one.
    /// </summary>
    [DataField]
    public bool? TargetState;

    /// <summary>
    /// Will the action fail if the target entity does not have a WieldableComponent?
    /// </summary>
    [DataField]
    public bool FailIfNoComp;
}

public sealed partial class WieldActionSystem : GoapActionSystem<Wield>
{
    [Dependency] private WieldableSystem _wield = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Wield action)
    {
        if (!TryGet(ent, action.TargetKey, out var target))
            return false;

        if (!TryComp(target, out WieldableComponent? comp))
        {
            ComponentNotFound<WieldableComponent>();
            return !action.FailIfNoComp;
        }

        if (comp.Wielded == action.TargetState)
            return true;

        if (comp.Wielded
            && action.TargetState != true
            && !_wield.TryUnwield(new(target, comp), ent))
            return false;

        if (!comp.Wielded
            && action.TargetState != false
            && !_wield.TryWield(new(target, comp), ent))
            return false;

        return true;
    }
}
