using Robust.Shared.GameStates;
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
    /// A sprite that will be rendered on every tile in the zone.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SpriteSpecifier? TileSprite;

    /// <summary>
    /// A sprite that will be rendered on every tile in the zone, if the zone is selected by the player.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SpriteSpecifier? SelectedTileSprite;

    /// <summary>
    /// The color that will be applied to all content within the zone.
    /// Is <see cref="TileSprite"/> or <see cref="SelectedTileSprite"/> not null,
    /// it will be used as modulation for them.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? ZoneColor;

    /// <summary>
    /// The color used to draw the zone's border.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? BorderColor;

    /// <summary>
    /// The value by which the <see cref="ZoneColor"/> and
    /// <see cref="BorderColor"/> will be multiplied if the zone is selected.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SelectedColorFactor = 1.15f;

    /// <summary>
    /// The thickness of the zone border, in world coordinates.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BorderThickness;

    /// <summary>
    /// The thickness of the zone's border, if the zone is selected by the player.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SelectedBorderThickness;

    /// <summary>
    /// Will small lines be displayed along the tile boundaries inside the zone?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ShowTileBorders;
}
