using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Weapons.Melee;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Rates melee weapons based on the amount of damage they deal.
/// </summary>
public sealed partial class MeleeDamage : BaseSearchConsideration<MeleeDamage>;

public sealed class MeleeDamageConsiderationSystem : NpcSearchConsiderationSystem<MeleeDamage>
{
    [Dependency] private readonly EntityQuery<MeleeWeaponComponent> _meleeQuery = default!;

    protected override float GetScore(GoapState state, EntityUid target, MeleeDamage con)
        => !_meleeQuery.TryComp(target, out var comp) ? 0f : comp.Damage.GetTotal().Float();
}
