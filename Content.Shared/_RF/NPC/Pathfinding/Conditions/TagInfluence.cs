using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared._RF.NPC.Pathfinding.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding.Conditions;

/// <summary>
/// The tag influences the node: the node is within the tag's radius, but not necessarily on the tag.
/// </summary>
public sealed partial class TagInfluence : BasePathCondition<TagInfluence>
{
    [DataField(required: true)]
    public ProtoId<PathTagPrototype> Tag;

    /// <summary>
    /// The influence must be greater than this value.
    /// </summary>
    [DataField]
    public float? MoreThan;

    /// <summary>
    /// The influence must be less than this value.
    /// </summary>
    [DataField]
    public float? LessThan;

    /// <summary>
    /// The name of the variable to which the strength of the influence will be written.
    /// </summary>
    [DataField]
    public string Var = "influence";
}

public sealed class TagInfluencePathConditionSystem : PathConditionSystem<TagInfluence>
{
    protected override bool ConditionCheck(PathNodeContext ctx, TagInfluence condition)
    {
        var influence = Pathfinding.GetInfluence(ctx.Facts, condition.Tag);

        if (condition.MoreThan != null && influence <= condition.MoreThan
            || condition.LessThan != null && influence >= condition.LessThan)
            return false;

        ctx.Variables[condition.Var] = influence;
        return true;
    }
}
