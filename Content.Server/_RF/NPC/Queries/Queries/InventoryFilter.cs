using Content.Server.NPC.Queries.Queries;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// Filters entities in the inventory
/// </summary>
public sealed partial class InventoryFilter : UtilityQueryFilter
{
    [DataField]
    public bool Invert;
}
