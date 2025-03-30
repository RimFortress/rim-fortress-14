using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Hands.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Pulls the bolt of the gun in his hand, if any
/// </summary>
public sealed partial class SwitchBoltClosedOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private SharedGunSystem _gun = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _gun = sysManager.GetEntitySystem<SharedGunSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<Hand>(NPCBlackboard.ActiveHand, out var hand, _entManager)
            || hand.HeldEntity is not { } entity
            || !_entManager.TryGetComponent(entity, out ChamberMagazineAmmoProviderComponent? chamber)
            || chamber.BoltClosed == null)
            return HTNOperatorStatus.Failed;

        _gun.SetBoltClosed(entity, chamber, !chamber.BoltClosed.Value);
        return HTNOperatorStatus.Finished;
    }
}
