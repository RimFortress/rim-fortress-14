using System.Diagnostics.CodeAnalysis;
using Content.Client._RF.UserInterface.Controls.Tooltip;
using Content.Client._RF.UserInterface.Prototypes;
using Content.Shared.Maps;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.UserInterface.Controls.RichText;

/// <summary>
/// Markup tag for hoverable tooltip terms.
/// </summary>
/// <example>
/// The tag supports 3 modes:<br/>
/// 1. Displaying a tooltip from a <see cref="TooltipPrototype"/>:
/// <c>[tooltip id="TooltipProtoId"]</c><br/>
/// 2. Displaying a tooltip from a <see cref="ContentTileDefinition"/>:
/// <c>[tooltip tile="TileProtoId"]</c><br/>
/// 2. Displaying a tooltip from a <see cref="EntityPrototype"/>:
/// <c>[tooltip entity="EntProtoId"]</c><br/>
/// <br/>
/// By default, the tooltip title text is inserted in place
/// of the tag, but you can insert your own text using:<br/>
/// <c>[tooltip="Tooltip text" id="TooltipProtoId"]</c>
/// </example>
public sealed partial class TooltipTag : IMarkupTagHandler
{
    [Dependency] private IPrototypeManager _proto = default!;

    private const string IdParam = "id";
    private const string TileParam = "tile";
    private const string EntParam = "entity";

    public string Name => "tooltip";

    /// <inheritdoc/>
    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        node.Value.TryGetString(out var text);

        if (node.Attributes.TryGetValue(IdParam, out var idParam)
            && idParam.TryGetString(out var tooltipId)
            && _proto.TryIndex<TooltipPrototype>(tooltipId, out var protoDef))
        {
            control = new TooltipTermLabel(protoDef)
            {
                Text = text ?? protoDef.Title,
            };
            return true;
        }

        if (node.Attributes.TryGetValue(TileParam, out var tileParam)
            && tileParam.TryGetString(out var tileId)
            && _proto.TryIndex<ContentTileDefinition>(tileId, out var tile))
        {
            var def = new TooltipDefinition
            {
                Title = Loc.GetString(tile.Name),
                TexturePath = tile.Sprite,
                IconType = TooltipIconType.Tile,
                CanIconFocus = true,
            };
            control = new TooltipTermLabel(def)
            {
                Text = text ?? def.Title,
            };
            return true;
        }

        if (node.Attributes.TryGetValue(EntParam, out var entParam)
            && entParam.TryGetString(out var entId)
            && _proto.TryIndex<EntityPrototype>(entId, out var ent))
        {
            var def = new TooltipDefinition
            {
                Title = ent.Name,
                Body = ent.Description,
                EntIcon = ent,
                CanIconFocus = true,
            };
            control = new TooltipTermLabel(def)
            {
                Text = text ?? def.Title,
            };
            return true;
        }

        control = null;
        return false;
    }
}
