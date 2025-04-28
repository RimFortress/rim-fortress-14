using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Damage;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the total amount of damage to the entity
/// </summary>
public sealed partial class TotalDamagePrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid? uid, _entManager)
            || !_entManager.TryGetComponent(uid, out DamageableComponent? damageable))
            return Invert;

        if (MoreThan != null && damageable.TotalDamage.Float() > MoreThan)
            return !Invert;

        if (LessThan != null && damageable.TotalDamage.Float() < LessThan)
            return !Invert;

        return Invert;
    }
}
