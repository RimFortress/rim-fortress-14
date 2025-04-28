using Content.Server.NPC.Queries.Queries;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// Filters items for healing by damage container and the type of damage it heals
/// </summary>
public sealed partial class HealingFilter : UtilityQueryFilter
{
    [DataField]
    public string DamageContainerKey = "DamageContainer";

    [DataField]
    public string DamageTypeKey = "DamageType";

    [DataField]
    public bool Invert;
}
