using Content.Server.NPC;
using Content.Shared.Damage;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the total amount of damage to the entity
/// </summary>
public sealed partial class TotalDamagePrecondition : InvertiblePrecondition
{
    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
            || !EntityManager.TryGetComponent(uid, out DamageableComponent? damageable))
            return false;

        return MoreThan != null && damageable.TotalDamage.Float() > MoreThan
               || LessThan != null && damageable.TotalDamage.Float() < LessThan;
    }
}
