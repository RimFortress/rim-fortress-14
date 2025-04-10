using Content.Server.NPC.Queries.Queries;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// Filters tools with a certain quality
/// </summary>
public sealed partial class ToolQualityFilter : UtilityQueryFilter
{
    /// <summary>
    /// Quality to be filtered out
    /// </summary>
    [DataField(required: true)]
    public string QualityKey = default!;

    [DataField]
    public bool Invert;
}
