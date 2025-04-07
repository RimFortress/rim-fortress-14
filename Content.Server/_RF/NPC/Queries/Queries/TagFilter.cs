using Content.Server.NPC.Queries.Queries;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// Filters the entities for the presence of tags
/// </summary>
public sealed partial class TagFilter : UtilityQueryFilter
{
    /// <summary>
    /// Tags to be filtered out
    /// </summary>
    [DataField(required: true)]
    public string TagsKey = default!;

    /// <summary>
    /// Do you need to check for all tags or just one tag
    /// </summary>
    [DataField(required: true)]
    public string RequireAllKey = default!;

    [DataField]
    public bool Invert;
}
