using Content.Server.Hands.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._RF.NPC.HTN.Operators.Interaction;

/// <summary>
/// Pulls the bolt of the gun in his hand, if any
/// </summary>
public sealed partial class SwitchBoltClosedOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private SharedGunSystem _gun = default!;
    private HandsSystem _hands = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _gun = sysManager.GetEntitySystem<SharedGunSystem>();
        _hands = sysManager.GetEntitySystem<HandsSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_hands.TryGetActiveItem(owner, out var heldEntity)
            || !_entManager.TryGetComponent(heldEntity, out ChamberMagazineAmmoProviderComponent? chamber)
            || chamber.BoltClosed == null)
            return HTNOperatorStatus.Failed;

        _gun.SetBoltClosed(heldEntity.Value, chamber, !chamber.BoltClosed.Value);
        return HTNOperatorStatus.Finished;
    }
}
