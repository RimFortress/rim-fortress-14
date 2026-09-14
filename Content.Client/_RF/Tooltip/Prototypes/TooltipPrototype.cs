using Content.Client._RF.Tooltip.RichText;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.Tooltip.Prototypes;

/// <summary>
/// A prototype of a tooltip that can be added when the cursor hovers over text using <see cref="TooltipTag"/>.
/// </summary>
[Prototype]
public sealed partial class TooltipPrototype : IPrototype, ITooltipDefinition
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <inheritdoc/>
    public string Title => Loc.GetString($"tooltip-{CaseConversion.PascalToKebab(ID)}-title");

    /// <inheritdoc/>
    public string Body => Loc.GetString($"tooltip-{CaseConversion.PascalToKebab(ID)}-body");

    /// <inheritdoc/>
    [DataField]
    public EntProtoId? EntIcon { get; set; }

    /// <inheritdoc/>
    [DataField]
    public ResPath? TexturePath { get; set; }

    /// <inheritdoc/>
    [DataField]
    public TooltipIconType IconType { get; set; } = TooltipIconType.Base;

    /// <inheritdoc/>
    [DataField]
    public bool CanIconFocus { get; set; }

    /// <inheritdoc/>
    public NetEntity? UidIcon => null;

    /// <inheritdoc/>
    public Control? ControlAfter => null;
}

public sealed class TooltipDefinition : ITooltipDefinition
{
    /// <inheritdoc/>
    public required string Title { get; set; }

    /// <inheritdoc/>
    public string? Body { get; set; }

    /// <inheritdoc/>
    public EntProtoId? EntIcon { get; set; }

    /// <inheritdoc/>
    public NetEntity? UidIcon { get; set; }

    /// <inheritdoc/>
    public ResPath? TexturePath { get; set; }

    /// <inheritdoc/>
    public TooltipIconType IconType { get; set; } = TooltipIconType.Base;

    /// <inheritdoc/>
    public bool CanIconFocus { get; set; }

    /// <inheritdoc/>
    public Control? ControlAfter { get; set; }
}

public interface ITooltipDefinition
{
    /// <summary>
    /// Tooltip title.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Main text of the tooltip.
    /// </summary>
    string? Body { get; }

    /// <summary>
    /// A prototype of an entity whose sprite will be used as an icon.
    /// </summary>
    EntProtoId? EntIcon { get; }

    /// <summary>
    /// An existing entity that will be used as a tooltip icon.
    /// </summary>
    NetEntity? UidIcon { get; }

    /// <summary>
    /// The path to the texture that will be used as the tooltip icon.
    /// </summary>
    ResPath? TexturePath { get; }

    /// <summary>
    /// Display type for tooltip icons.
    /// </summary>
    TooltipIconType IconType { get; }

    /// <summary>
    /// If true, the user will be able to view the tooltip icon in high resolution by hovering over it.
    /// </summary>
    bool CanIconFocus { get; }

    /// <summary>
    /// A control that will be added after the tooltip body text.
    /// </summary>
    Control? ControlAfter { get; }
}

[Serializable]
public enum TooltipIconType
{
    /// <summary>
    /// Default display: icon centered, with the icon taking up as much space as possible.
    /// </summary>
    Base,

    /// <summary>
    /// A special mode for tile textures, where the texture fills all available space and part of the texture is cropped.
    /// </summary>
    Tile,
}
