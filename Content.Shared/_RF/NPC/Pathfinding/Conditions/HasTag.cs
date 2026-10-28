using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared._RF.NPC.Pathfinding.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding.Conditions;

/// <summary>
/// The tag is located on the node itself.
/// </summary>
public sealed partial class HasTag : BasePathCondition<HasTag>
{
    [DataField(required: true)]
    public ProtoId<PathTagPrototype> Tag;
}

public sealed class HasTagPathConditionSystem : PathConditionSystem<HasTag>
{
    protected override bool ConditionCheck(PathNodeContext ctx, HasTag condition)
        => Pathfinding.HasTag(ctx.Facts, condition.Tag);
}
