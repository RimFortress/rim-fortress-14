using Content.Server.NPC;
using Content.Shared.Weapons.Melee;

namespace Content.Server._RF.NPC.Queries.Filters;

/// <summary>
/// Filters melee weapons by damage type
/// </summary>
public sealed partial class MeleeDamageTypeFilter : RfUtilityQueryFilter
{
    private EntityQuery<MeleeWeaponComponent> _meleeQuery;

    [DataField(required: true)]
    public string DamageType = default!;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _meleeQuery = entManager.GetEntityQuery<MeleeWeaponComponent>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _meleeQuery.TryComp(uid, out var comp)
               && comp.Damage.DamageDict.ContainsKey(DamageType);
    }
}
