using Content.Shared._RF.MathHelpers.MathCurve;
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
    /// For a dynamic tag: how much a source counts for, calculated with the entity that has the tag as the user. The influence on a
    /// node is the sum of those of the sources, each as much as this and as the shape of the tag says for the distance to it. An entity
    /// that gets nothing (not more than 0) has no influence, such as a resident who is down. 1 if it is not set.
    /// </summary>
    [DataField]
    public List<MathCurve> Weight = new();

    /// <summary>
    /// For a dynamic tag: how far a source influences, in tiles, calculated with the entity that has the tag as the user: the shape of
    /// the tag is stretched to it. It is how a source can be as dangerous as far as it shoots. The radius of the tag if it is not set.
    /// </summary>
    [DataField]
    public List<MathCurve> Reach = new();

    /// <summary>
    /// For a dynamic tag: the influence is only where the source can be seen from, the nodes behind something that stops projectiles (a
    /// wall, a window, a closed door) are out of it. It is a line of fire, and not a place around the source.
    /// </summary>
    [DataField]
    public bool Sight;

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
        var influence = Pathfinding.GetInfluence(ctx, condition);

        if (condition.MoreThan != null && influence <= condition.MoreThan
            || condition.LessThan != null && influence >= condition.LessThan)
            return false;

        ctx.Variables[condition.Var] = influence;
        return true;
    }
}
