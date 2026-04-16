using Content.Server.NPC;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the total amount of damage to the entity
/// </summary>
public sealed partial class TotalDamagePrecondition : InvertiblePrecondition
{
    private DamageableSystem _damageable = default!;

    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _damageable = sysManager.GetEntitySystem<DamageableSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
            || !EntityManager.TryGetComponent(uid, out DamageableComponent? damageable))
            return false;

        var total = _damageable.GetTotalDamage(new(uid.Value, damageable));
        return total > MoreThan || total < LessThan;
    }
}
