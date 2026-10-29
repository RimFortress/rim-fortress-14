using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding.Systems;

// Damage that the agent is able to deal.
public abstract partial class SharedRfPathfindingSystem
{
    [Dependency] private ItemSlotsSystem _slots = null!;
    [Dependency] private SharedContainerSystem _container = null!;
    [Dependency] private SharedNpcSearcherSystem _searcher = null!;
    [Dependency] private EntityWhitelistSystem _whitelist = null!;

    [Dependency] private EntityQuery<MeleeWeaponComponent> _meleeQuery;
    [Dependency] private EntityQuery<GunComponent> _gunQuery;
    [Dependency] private EntityQuery<ItemSlotsComponent> _itemSlotsQuery;
    [Dependency] private EntityQuery<BallisticAmmoProviderComponent> _ballisticQuery;
    [Dependency] private EntityQuery<BasicEntityAmmoProviderComponent> _basicAmmoQuery;

    // Search queries whose results are kept up to date by the searcher.
    private static readonly ProtoId<SearchQueryPrototype> MeleesQuery = "PathMelees";
    private static readonly ProtoId<SearchQueryPrototype> GunsQuery = "PathGuns";
    private static readonly ProtoId<SearchQueryPrototype> MagazinesQuery = "PathMagazines";

    // A buffer that is reused by the calculations.
    private readonly List<(float Progress, float PerSecond, int Count)> _plan = new();

    /// <summary>
    /// Collects all the ways the agent can deal damage with what it holds and carries: melee weapons, which
    /// cannot run out, and firearms, whose attacks are limited by the ammunition in them and by the spare
    /// magazines the agent carries.
    /// </summary>
    /// <remarks>
    /// The weapons are taken from the results of the search queries, which the searcher keeps up to date,
    /// so the inventory is not searched here. Only the ammunition is counted at the time of the call.
    /// Only firearms that shoot cartridges or entities with a projectile are taken into account.
    /// Reloading takes no time here, and burst and automatic fire are not distinguished.
    /// </remarks>
    /// <param name="agent">The agent whose attacks are collected.</param>
    /// <param name="attacks">The list to which the attacks are added.</param>
    [PublicAPI]
    public void CollectAttacks(Entity<GoapComponent> agent, List<PathAttack> attacks)
    {
        // Bare hands, claws and the like.
        AddMelee(agent, agent, attacks);

        foreach (var weapon in _searcher.GetResults(agent, MeleesQuery))
        {
            AddMelee(agent, weapon, attacks);
        }

        var magazines = _searcher.GetResults(agent, MagazinesQuery);

        foreach (var gun in _searcher.GetResults(agent, GunsQuery))
        {
            AddGun(gun, magazines, attacks);
        }
    }

    private void AddMelee(EntityUid agent, EntityUid weapon, List<PathAttack> attacks)
    {
        if (!_meleeQuery.TryComp(weapon, out var melee))
            return;

        var rate = _melee.GetAttackRate(weapon, agent, melee);
        var damage = _melee.GetDamage(weapon, agent, melee);

        if (rate > 0f && damage.GetTotal() > FixedPoint2.Zero)
            attacks.Add(new PathAttack(damage, rate, PathAttack.Unlimited));
    }

    private void AddGun(EntityUid gun, IReadOnlyList<EntityUid> magazines, List<PathAttack> attacks)
    {
        if (!_gunQuery.TryComp(gun, out var gunComp))
            return;

        var rate = gunComp.FireRateModified > 0f ? gunComp.FireRateModified : gunComp.FireRate;
        var loaded = new GetAmmoCountEvent();
        RaiseLocalEvent(gun, ref loaded);

        var count = loaded.Count;
        EntityUid? sample = null;

        // Magazines the agent carries, that fit the gun, are additional shots.
        if (_itemSlotsQuery.TryComp(gun, out var itemSlots)
            && _slots.TryGetSlot(new(gun, itemSlots), SharedGunSystem.MagazineSlot, out var slot))
        {
            sample = slot.Item;

            foreach (var item in magazines)
            {
                // A magazine inside a firearm is not a spare one, the ammunition of the gun it is in is counted
                // with that gun.
                if (!_whitelist.CheckBoth(item, slot.Blacklist, slot.Whitelist)
                    || _container.TryGetContainingContainer(item, out var container) && _gunQuery.HasComp(container.Owner))
                    continue;

                var spare = new GetAmmoCountEvent();
                RaiseLocalEvent(item, ref spare);

                if (spare.Count <= 0)
                    continue;

                count += spare.Count;
                sample ??= item;
            }
        }

        if (rate <= 0f || count <= 0)
            return;

        var damage = GetShotDamage(gun) ?? (sample is { } ammo ? GetShotDamage(ammo) : null);

        if (damage != null && damage.GetTotal() > FixedPoint2.Zero)
            attacks.Add(new PathAttack(damage, rate, count));
    }

    /// <summary>
    /// Returns the damage of a shot from an ammunition provider (a firearm with built-in ammunition,
    /// or a magazine), null if it is unknown.
    /// </summary>
    private DamageSpecifier? GetShotDamage(EntityUid provider)
    {
        if (_ballisticQuery.TryComp(provider, out var ballistic))
        {
            foreach (var cartridge in ballistic.Entities)
            {
                if (MetaData(cartridge).EntityPrototype is { } proto)
                    return GetProjectileDamage(proto.ID);
            }

            return ballistic.Proto == null ? null : GetProjectileDamage(ballistic.Proto.Value);
        }

        return _basicAmmoQuery.TryComp(provider, out var basic) ? GetProjectileDamage(basic.Proto) : null;
    }

    /// <summary>
    /// Returns the damage of a projectile or of a cartridge that shoots a projectile.
    /// </summary>
    private DamageSpecifier? GetProjectileDamage(EntProtoId proto)
    {
        if (!_proto.TryIndex(proto, out var prototype))
            return null;

        if (prototype.TryComp<CartridgeAmmoComponent>(out var cartridge, EntityManager.ComponentFactory)
            && !_proto.TryIndex(cartridge.Prototype, out prototype))
            return null;

        return prototype.TryComp<ProjectileComponent>(out var projectile, EntityManager.ComponentFactory)
            ? projectile.Damage
            : null;
    }

    /// <summary>
    /// Returns the time in seconds the agent needs to deal the damage that is required to destroy something,
    /// <see cref="float.PositiveInfinity"/> if it is not able to.
    /// </summary>
    /// <remarks>
    /// Every attack brings the target closer to destruction proportionally to what the target needs, so the
    /// whole destruction is 1 and an attack that deals only the damage types the target is not damaged by
    /// brings nothing. Attacks are used from the fastest to the slowest, each until its ammunition runs out.
    /// The agent does not spend time on switching weapons and reloading.
    /// </remarks>
    /// <param name="required">Damage that destroys the target, per damage type.</param>
    /// <param name="attacks">The attacks of the agent, see <see cref="CollectAttacks"/>.</param>
    [PublicAPI, Pure]
    public float GetDestroyTime(DamageSpecifier required, List<PathAttack> attacks)
    {
        _plan.Clear();

        foreach (var attack in attacks)
        {
            var progress = 0f;

            foreach (var (type, needed) in required.DamageDict)
            {
                if (needed > FixedPoint2.Zero
                    && attack.Damage.DamageDict.TryGetValue(type, out var dealt)
                    && dealt > FixedPoint2.Zero)
                    progress += dealt.Float() / needed.Float();
            }

            if (progress > 0f && attack.Rate > 0f)
                _plan.Add((progress, progress * attack.Rate, attack.Count));
        }

        _plan.Sort((a, b) => b.PerSecond.CompareTo(a.PerSecond));

        var remaining = 1f;
        var time = 0f;

        foreach (var (progress, perSecond, count) in _plan)
        {
            var dealt = MathF.Min(remaining, progress * count);
            time += dealt / perSecond;
            remaining -= dealt;

            if (remaining <= 0.0001f)
                return time;
        }

        return float.PositiveInfinity;
    }
}
