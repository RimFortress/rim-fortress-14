using Content.Shared._RF.Skills.Prototypes;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Skills;

/// <summary>
/// Makes the damage and the attack rate of melee weapons depend on the skill of the one who hits.
/// The check of the hit itself is done where the hit is, see <see cref="SharedMeleeWeaponSystem"/>.
/// </summary>
public sealed partial class MeleeSkillsSystem : EntitySystem
{
    [Dependency] private SharedSkillsSystem _skills = null!;

    /// <summary>
    /// A hit of a melee weapon or fists.
    /// </summary>
    public static readonly ProtoId<SkillQueryPrototype> MeleeAttackQuery = "MeleeAttack";

    [SubscribeLocalEvent]
    private void OnGetMeleeDamage(Entity<MeleeWeaponComponent> ent, ref GetMeleeDamageEvent args)
    {
        args.Damage *= _skills.GetResult(MeleeAttackQuery, args.User, ent, 1f);
    }

    [SubscribeLocalEvent]
    private void OnGetMeleeAttackRate(Entity<MeleeWeaponComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        args.Rate = _skills.GetDelay(MeleeAttackQuery, args.User, ent, args.Rate);
    }
}
