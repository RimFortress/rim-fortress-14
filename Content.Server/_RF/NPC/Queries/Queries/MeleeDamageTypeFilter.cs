using Content.Server.NPC.Queries.Queries;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// Filters melee weapons by damage type
/// </summary>
public sealed partial class MeleeDamageTypeFilter : UtilityQueryFilter
{
    [DataField(required: true)]
    public string DamageType = default!;

    [DataField]
    public bool Invert;
}
