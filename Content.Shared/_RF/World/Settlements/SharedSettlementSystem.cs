using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Shared._RF.World.Settlements;

/// <summary>
/// The settlements of the players, see <see cref="SettlementTrackerComponent"/> for how they are found. The server finds
/// them, and the clients see the ones that are loaded.
/// </summary>
public abstract partial class SharedSettlementSystem : EntitySystem
{
    [Dependency] private EntityQuery<SettlementComponent> _settlementQuery;
    [Dependency] private EntityQuery<SettlementResidentComponent> _residentQuery;

    /// <summary>
    /// Returns the settlements of a player, found again first if something has changed.
    /// </summary>
    /// <param name="player">The player entity.</param>
    /// <param name="includeDissolving">Are the settlements whose group is gone, and that wait to be disbanded, returned.</param>
    [PublicAPI]
    public List<Entity<SettlementComponent>> GetPlayerSettlements(EntityUid player, bool includeDissolving = false)
    {
        Refresh(player);
        return FindSettlements(player, includeDissolving);
    }

    /// <summary>
    /// Returns the settlements of a player as they are now, without finding them again.
    /// </summary>
    protected List<Entity<SettlementComponent>> FindSettlements(EntityUid player, bool includeDissolving)
    {
        var result = new List<Entity<SettlementComponent>>();
        var query = EntityQueryEnumerator<SettlementComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Player == player && (includeDissolving || !comp.Dissolving))
                result.Add((uid, comp));
        }

        return result;
    }

    /// <summary>
    /// Returns the settlements of all the players, found again first if something has changed.
    /// </summary>
    /// <param name="includeDissolving">Are the settlements whose group is gone, and that wait to be disbanded, returned.</param>
    [PublicAPI]
    public List<Entity<SettlementComponent>> AllSettlements(bool includeDissolving = false)
    {
        var trackers = EntityQueryEnumerator<SettlementTrackerComponent>();

        while (trackers.MoveNext(out var player, out _))
        {
            Refresh(player);
        }

        var result = new List<Entity<SettlementComponent>>();
        var query = EntityQueryEnumerator<SettlementComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (includeDissolving || !comp.Dissolving)
                result.Add((uid, comp));
        }

        return result;
    }

    /// <summary>
    /// Finds the settlement a pop lives in. Only the server knows it.
    /// </summary>
    [PublicAPI]
    public bool TryGetSettlement(EntityUid pop, [NotNullWhen(true)] out Entity<SettlementComponent>? settlement)
    {
        settlement = null;

        if (!_residentQuery.TryComp(pop, out var resident)
            || !_settlementQuery.TryComp(resident.Settlement, out var comp))
            return false;

        settlement = (resident.Settlement, comp);
        return true;
    }

    /// <summary>
    /// Returns the coordinates of the center of a settlement.
    /// </summary>
    [PublicAPI]
    public EntityCoordinates GetCoordinates(EntityUid settlement) => Transform(settlement).Coordinates;

    /// <summary>
    /// Returns the pops that live in a settlement, only the server knows them.
    /// </summary>
    [PublicAPI]
    public IReadOnlySet<EntityUid> GetResidents(Entity<SettlementComponent> settlement) => settlement.Comp.Residents;

    /// <summary>
    /// Gives a thing of the player to the settlement it is in, to be owned by it. If there is no settlement there, the player keeps
    /// the thing until there is.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="uid">The thing.</param>
    /// <param name="coordinates">Where it is.</param>
    /// <returns>The settlement that owns the thing, null if it is the player who does.</returns>
    [PublicAPI]
    public virtual Entity<SettlementComponent>? Claim(EntityUid player, EntityUid uid, EntityCoordinates coordinates) => null;

    /// <summary>
    /// Makes sure the settlements of a player are up to date: if something has changed, or it has been a while, they are
    /// found again. Only the server does something.
    /// </summary>
    [PublicAPI]
    public virtual void Refresh(EntityUid player)
    {
    }

    /// <summary>
    /// Tells that something has changed, and the settlements of a player are to be found again as soon as possible. Only the
    /// server does something.
    /// </summary>
    [PublicAPI]
    public virtual void MarkDirty(EntityUid player)
    {
    }
}
