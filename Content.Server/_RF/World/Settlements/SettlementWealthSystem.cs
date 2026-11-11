using System.Linq;
using Content.Server.Cargo.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.World.Settlements;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._RF.World.Settlements;

/// <summary>
/// Keeps the wealth of the settlements, see <see cref="SettlementWealthComponent"/>: the price of what a settlement owns, by the
/// categories that the prototype of the settlement tells. It is counted when what the settlement owns has changed, not more often than
/// a short while apart, and every so often even if it has not: the prices of the things change by themselves.
/// </summary>
public sealed partial class SettlementWealthSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private PricingSystem _pricing = null!;
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private EntityWhitelistSystem _whitelist = null!;
    [Dependency] private SharedContainerSystem _container = null!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SettlementWealthComponent>();

        while (query.MoveNext(out var uid, out var wealth))
        {
            if (now < wealth.NextUpdate)
                continue;

            if (wealth.SettlementDirty || now - wealth.LastUpdate >= wealth.UpdateInterval)
                Recount((uid, wealth));
        }
    }

    [SubscribeLocalEvent]
    private void OnOwnershipAdded(Entity<SettlementWealthComponent> ent, ref OwnershipAddedEvent args)
    {
        // The event is raised for the owner and for the owned.
        if (args.Owner == ent.Owner)
            ent.Comp.SettlementDirty = true;
    }

    [SubscribeLocalEvent]
    private void OnOwnershipRemoved(Entity<SettlementWealthComponent> ent, ref OwnershipRemovedEvent args)
    {
        if (args.Owner == ent.Owner)
            ent.Comp.SettlementDirty = true;
    }

    /// <summary>
    /// The price of a category of what the settlement owns, as it was counted last.
    /// </summary>
    [PublicAPI]
    public double GetValue(Entity<SettlementWealthComponent> settlement, string category)
        => settlement.Comp.Values.GetValueOrDefault(category);

    /// <summary>
    /// The wealth of the settlement as it was counted last, all the categories together as they are priced.
    /// </summary>
    [PublicAPI]
    public double GetTotal(Entity<SettlementWealthComponent> settlement)
        => settlement.Comp.Values.Values.Sum();

    /// <summary>
    /// Counts the wealth now.
    /// </summary>
    [PublicAPI]
    public void Recount(Entity<SettlementWealthComponent> settlement)
    {
        var comp = settlement.Comp;
        var now = _timing.CurTime;

        comp.Values.Clear();
        comp.Counts.Clear();

        foreach (var category in comp.Categories)
        {
            comp.Values[category.Id] = 0;
            comp.Counts[category.Id] = 0;
        }

        foreach (var uid in _ownership.GetOwned(settlement.Owner))
        {
            if (TerminatingOrDeleted(uid)
                || HasComp<SettlementComponent>(uid)
                // The price of a thing is the price of what is in it as well, so what is in it is not counted twice.
                || _container.TryGetContainingContainer(uid, out var container)
                && _ownership.HasOwner(container.Owner, settlement.Owner))
                continue;

            var category = Categorize(comp, uid);

            if (category == null)
                continue;

            comp.Values[category] += _pricing.GetPrice(uid);
            comp.Counts[category]++;
        }

        comp.SettlementDirty = false;
        comp.LastUpdate = now;
        comp.NextUpdate = now + comp.MinUpdateInterval;
    }

    /// <summary>
    /// The category of the wealth that the entity falls into, null if it falls into none.
    /// </summary>
    [PublicAPI]
    public string? Categorize(SettlementWealthComponent comp, EntityUid uid)
    {
        foreach (var category in comp.Categories)
        {
            if (_whitelist.CheckBoth(uid, category.Blacklist, category.Whitelist))
                return category.Id;
        }

        return null;
    }
}
