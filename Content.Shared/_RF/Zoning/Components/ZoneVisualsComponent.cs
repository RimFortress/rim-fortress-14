using JetBrains.Annotations;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Zoning.Components;

/// <summary>
/// A component that specifies the display settings for the zone on the client.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(fieldDeltas: true, raiseAfterAutoHandleState: true)]
public sealed partial class ZoneVisualsComponent : Component
{
    /// <summary>
    /// Can the component properties be changed by the user - zone owner?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Editable;

    /// <summary>
    /// Zone icon in the interface.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Current zone visualization mode. Can be changed only on the client.
    /// </summary>
    [DataField]
    public ZoneVisualsState CurrentState = ZoneVisualsState.Base;

    /// <summary>
    /// The color used to display the selection border for this zone.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? SelectionBorderColor;

    /// <summary>
    /// The color used to display the inner selection area of this zone.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? SelectionInnerColor;

    /// <summary>
    /// Per-state overrides layered. Each entry should
    /// specify ONLY the fields that differ from the base for that state - unset
    /// (null) fields fall through to <see cref="ZoneVisualsState.Base"/> (see <see cref="GetStyle"/>).
    /// Keys are expected to be single flags, not combinations; if multiple flags
    /// are active at once, they're layered together by <see cref="GetStyle"/> instead
    /// of requiring a separate entry for every combination.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ZoneVisualsState, ZoneVisualsStyle> States = new();

    /// <summary>
    /// Priority order for layering when multiple state flags are active simultaneously -
    /// earlier entries win over later ones for any field they both set.
    /// </summary>
    public static readonly ZoneVisualsState[] PriorityOrder = new[]
    {
        ZoneVisualsState.Base,
        ZoneVisualsState.Invalid,
        ZoneVisualsState.Picking,
        ZoneVisualsState.Selected,
    };

    /// <summary>
    /// Resolves the effective visuals for the given combination of active state flags.
    /// (highest priority in <see cref="PriorityOrder"/> wins per-field, falling back to
    /// lower-priority flags).
    /// </summary>
    [PublicAPI, Pure]
    public ZoneVisualsStyle GetStyle()
    {
        var result = new ZoneVisualsStyle();

        // Walk lowest to highest priority so higher-priority flags overwrite last.
        foreach (var flag in PriorityOrder)
        {
            if ((CurrentState & flag) == 0 || !States.TryGetValue(flag, out var over))
                continue;

            result = result.LayeredWith(over);
        }

        return result;
    }
}

/// <summary>
/// A set of visual fields for a zone. All fields are optional (null = "not set, fall
/// through to whatever this is layered on top of" - see <see cref="ZoneVisualsComponent.GetStyle"/>),
/// except <see cref="ShowTileBorders"/>, which need a concrete
/// value once fully resolved and so default to sensible bases.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public partial record struct ZoneVisualsStyle
{
    /// <summary>
    /// A sprite that will be rendered on every tile in the zone.
    /// </summary>
    [DataField]
    public SpriteSpecifier? TileSprite;

    /// <summary>
    /// The color that will be applied to all content within the zone.
    /// If <see cref="TileSprite"/> is not null, it will be used as modulation for it.
    /// </summary>
    [DataField]
    public Color? ZoneColor;

    /// <summary>
    /// The color used to draw the zone's border.
    /// </summary>
    [DataField]
    public Color? BorderColor;

    /// <summary>
    /// Will small lines be displayed along the tile boundaries inside the zone?
    /// </summary>
    [DataField]
    public bool? ShowTileBorders;

    /// <summary>
    /// Will a list of zone conditions and their status be displayed above the zone?
    /// </summary>
    [DataField]
    public bool? ShowConditions;

    /// <summary>
    /// Returns a copy of this style with every field <paramref name="over"/> sets
    /// overriding this style's corresponding field, and every field <paramref name="over"/>
    /// leaves null falling through to this style's value unchanged.
    /// </summary>
    public readonly ZoneVisualsStyle LayeredWith(ZoneVisualsStyle over) => new()
    {
        TileSprite = over.TileSprite ?? TileSprite,
        ZoneColor = over.ZoneColor ?? ZoneColor,
        BorderColor = over.BorderColor ?? BorderColor,
        ShowTileBorders = over.ShowTileBorders ?? ShowTileBorders,
        ShowConditions = over.ShowConditions ?? ShowConditions,
    };

    public ZoneVisualsStyle(ZoneVisualsStyle other)
    {
        TileSprite = other.TileSprite;
        ZoneColor = other.ZoneColor;
        BorderColor = other.BorderColor;
        ShowTileBorders = other.ShowTileBorders;
        ShowConditions = other.ShowConditions;
    }
}

[Flags]
[Serializable, NetSerializable]
public enum ZoneVisualsState : byte
{
    None = 0,

    /// <summary>
    /// Standard zone visualization - no overrides apply.
    /// </summary>
    Base = 1 << 0,

    /// <summary>
    /// Display mode when the user is in the zone selection mode for editing.
    /// </summary>
    Picking = 1 << 1,

    /// <summary>
    /// Display mode when the zone is in the user's current selection.
    /// </summary>
    Selected = 1 << 2,

    /// <summary>
    /// Display mode when not all conditions for creating a zone are met.
    /// </summary>
    Invalid = 1 << 3,
}
