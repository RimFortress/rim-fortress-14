using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Rates melee weapons based on the amount of damage they deal.
/// </summary>
public sealed partial class MeleeDamage : BaseSearchConsideration<MeleeDamage>
{
    /// <summary>
    /// Damage types to be included in the calculation; if empty, all damage will be counted.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DamageTypePrototype>> DamageTypes = new();
}

public sealed partial class MeleeDamageConsiderationSystem : NpcSearchConsiderationSystem<MeleeDamage>
{
    [Dependency] private readonly EntityQuery<MeleeWeaponComponent> _meleeQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SearchTrackedComponent, ItemToggledEvent>((ent, ref _) => Rescore(ent.AsNullable()));
    }

    protected override float GetScore(GoapState state, EntityUid target, MeleeDamage con)
    {
        if (!_meleeQuery.TryComp(target, out var comp))
            return 0f;

        if (con.DamageTypes.Count == 0)
            return comp.Damage.GetTotal().Float();

        var damage = 0f;

        foreach (var type in con.DamageTypes)
        {
            if (comp.Damage.DamageDict.TryGetValue(type, out var value))
                damage += value.Float();
        }

        return damage;
    }
}
