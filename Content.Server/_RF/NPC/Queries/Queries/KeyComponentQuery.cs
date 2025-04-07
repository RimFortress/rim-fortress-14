using Content.Server.NPC.Queries.Queries;

namespace Content.Server._RF.NPC.Queries.Queries;


public sealed partial class KeyComponentQuery : UtilityQuery
{
    /// <summary>
    /// Component to be filtered out
    /// </summary>
    [DataField(required: true)]
    public string Key = default!;

    [DataField]
    public bool Invert;
}
