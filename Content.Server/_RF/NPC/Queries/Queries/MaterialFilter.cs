using Content.Server.NPC.Queries.Queries;

namespace Content.Server._RF.NPC.Queries.Queries;

public sealed partial class MaterialFilter : UtilityQueryFilter
{
    /// <summary>
    /// Key containing the material to be filtered out
    /// </summary>
    [DataField(required: true)]
    public string TargetKey = default!;

    /// <summary>
    /// Key containing the minimum amount of material that will be filtered out
    /// </summary>
    [DataField(required: true)]
    public string AmountKey = default!;

    [DataField]
    public bool Invert;
}
