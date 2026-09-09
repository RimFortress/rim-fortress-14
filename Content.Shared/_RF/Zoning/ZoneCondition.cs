using Content.Shared._RF.Zoning.Systems;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning;

/// <summary>
/// Condition related to the zone's tiles/entities that
/// must be met for the zone to be created or to remain valid.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class ZoneCondition
{
    /// <summary>
    /// The number of tiles/entities in the zone that must meet the condition.
    /// </summary>
    [DataField]
    public int Amount = 1;

    /// <summary>
    /// Whether the result of the check will be inverted.
    /// </summary>
    [DataField]
    public bool Invert;

    public LocId Description => $"zone-condition-{CaseConversion.PascalToKebab(GetType().Name)}-desc";

    /// <summary>
    /// Type on this condition.
    /// </summary>
    public abstract ZoneConditionType Type { get; }

    /// <summary>
    /// Checks whether a target tile can be added to the zone.
    /// </summary>
    /// <param name="tile">Target tile.</param>
    /// <param name="checker"></param>
    [PublicAPI, Pure]
    public abstract bool TileValidCheck(TileRef tile, IZoneConditionChecker checker);

    /// <summary>
    /// Checks the condition against tiles in the zone that have
    /// already passed the <see cref="ZoneConditionType.TileAdd"/> condition.
    /// </summary>
    /// <param name="tiles">A list of all tiles in the zone.</param>
    /// <param name="checker"></param>
    [PublicAPI, Pure]
    public abstract bool TileCheck(IReadOnlySet<TileRef> tiles, IZoneConditionChecker checker);

    /// <summary>
    /// Checks the condition against entities in the zone.
    /// </summary>
    /// <param name="entities">A list of all entities in the zone.</param>
    /// <param name="checker"></param>
    [PublicAPI, Pure]
    public abstract bool EntityCheck(IReadOnlySet<EntityUid> entities, IZoneConditionChecker checker);

    /// <summary>
    /// Returns a description of the condition for the user.
    /// </summary>
    /// <param name="tile">
    /// Target tile to check; not null if the <see cref="Type"/> is <see cref="ZoneConditionType.TileAdd"/>.
    /// </param>
    /// <param name="tiles">A list of all tiles in the zone.</param>
    /// <param name="entities">A list of all entities in the zone.</param>
    /// <param name="checker"></param>
    [PublicAPI, Pure]
    public abstract ZoneConditionDesc ConditionDescription(
        TileRef? tile,
        IReadOnlySet<TileRef> tiles,
        IReadOnlySet<EntityUid> entities,
        IZoneConditionChecker checker);
}

public abstract partial class BaseZoneCondition<T> : ZoneCondition where T : BaseZoneCondition<T>
{
    public override bool TileValidCheck(TileRef tile, IZoneConditionChecker checker)
    {
        DebugTools.Assert(Type == ZoneConditionType.TileAdd);
        var result = checker.TileValidCheck((T)this, tile);
        return Invert ? !result : result;
    }

    public override bool TileCheck(IReadOnlySet<TileRef> tiles, IZoneConditionChecker checker)
    {
        DebugTools.Assert(Type == ZoneConditionType.Tile);
        var result = checker.TileCheck((T)this, tiles);
        return Invert ? !result : result;
    }

    public override bool EntityCheck(IReadOnlySet<EntityUid> entities, IZoneConditionChecker checker)
    {
        DebugTools.Assert(Type == ZoneConditionType.Entity);
        var result = checker.EntityCheck((T)this, entities);
        return Invert ? !result : result;
    }

    public override ZoneConditionDesc ConditionDescription(
        TileRef? tile,
        IReadOnlySet<TileRef> tiles,
        IReadOnlySet<EntityUid> entities,
        IZoneConditionChecker checker)
        => checker.ConditionDescription((T)this, tile, tiles, entities);
}

/// <param name="Text">Description text.</param>
/// <param name="IsMet">Is the condition met?</param>
/// <param name="Progress">Text describing the current progress in fulfilling the condition.</param>
[Serializable, NetSerializable]
public readonly record struct ZoneConditionDesc(string Text, bool IsMet, string? Progress = null)
{
    public static string? ProgressText(ILocalizationManager locale, int current, int amount)
        => amount > 1
            ? locale.GetString("zone-condition-base-progress", ("current", current), ("amount", amount))
            : null;
}

[Serializable, NetSerializable]
public enum ZoneConditionType : byte
{
    /// <summary>
    /// Conditions for a tile that must be met in order to add it to a zone.
    /// </summary>
    TileAdd,

    /// <summary>
    /// Conditions related to tiles that must be met for the zone to remain valid.
    /// </summary>
    Tile,

    /// <summary>
    /// Conditions relating to entities within the zone that must be satisfied for the zone to remain valid.
    /// </summary>
    Entity,
}
