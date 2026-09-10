using System.Diagnostics.CodeAnalysis;
using Content.Client._RF.Tooltip.Controls;
using Content.Client._RF.Tooltip.Prototypes;
using Content.Shared.Maps;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.Tooltip.RichText;

/// <summary>
/// Markup tag for hoverable tooltip terms.
/// </summary>
/// <example>
/// The tag supports 4 modes:<br/>
/// 1. Displaying a tooltip from a <see cref="TooltipPrototype"/>:
/// <c>[tooltip id="TooltipProtoId"]</c><br/>
/// 2. Displaying a tooltip from a <see cref="ContentTileDefinition"/>:
/// <c>[tooltip tile="TileProtoId"]</c><br/>
/// 2. Displaying a tooltip from a <see cref="EntityPrototype"/>:
/// <c>[tooltip entity="EntProtoId"]</c><br/>
/// 3. Displaying a tooltip from existing <see cref="NetEntity"/>:
/// <c>[tooltip netUid=1234]</c><br/>
/// <br/>
/// By default, the tooltip title text is inserted in place
/// of the tag, but you can insert your own text using:<br/>
/// <c>[tooltip="Tooltip text" id="TooltipProtoId"]</c>
/// </example>
public sealed partial class TooltipTag : IMarkupTagHandler
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private const string IdParam = "id";
    private const string TileParam = "tile";
    private const string EntParam = "entity";
    private const string NetUidParam = "netUid";

    public string Name => "tooltip";

    /// <inheritdoc/>
    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        node.Value.TryGetString(out var text);

        if (node.Attributes.TryGetValue(IdParam, out var idParam)
            && idParam.TryGetString(out var tooltipId)
            && _proto.TryIndex<TooltipPrototype>(tooltipId, out var protoDef))
        {
            control = new TooltipTermLabel(protoDef, text ?? protoDef.Title);
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
            control = new TooltipTermLabel(def, text ?? def.Title);
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
            control = new TooltipTermLabel(def, text ?? def.Title);
            return true;
        }

        if (node.Attributes.TryGetValue(NetUidParam, out var uidParam)
            && uidParam.TryGetLong(out var netUid)
            && _entity.TryGetEntity(new((int)netUid.Value), out var netEnt)
            && _entity.TryGetComponent(netEnt, out MetaDataComponent? meta))
        {
            var def = new TooltipDefinition
            {
                Title = meta.EntityName,
                Body = meta.EntityDescription,
                UidIcon = new NetEntity((int)netUid.Value),
                CanIconFocus = true,
            };
            control = new TooltipTermLabel(def, text ?? def.Title);
            return true;
        }

        control = null;
        return false;
    }
}
