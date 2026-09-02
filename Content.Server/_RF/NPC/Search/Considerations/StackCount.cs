using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Evaluates stacks of material based on their quantity.
/// </summary>
public sealed partial class StackCount : BaseSearchConsideration<StackCount>
{
    /// <summary>
    /// If true, the value will be normalized relative to the maximum amount of material in the stack.
    /// </summary>
    [DataField]
    public bool Normalize = true;
}

public sealed partial class StackCountSearchConsiderationSystem : NpcSearchConsiderationSystem<StackCount>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly EntityQuery<StackComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeRescoreEvent<StackCountChangedEvent>();
    }

    protected override float GetScore(GoapState state, EntityUid target, StackCount con)
    {
        if (!_query.TryComp(target, out var comp))
            return 0f;

        if (!con.Normalize)
            return comp.Count;

        var max = _proto.Index(comp.StackTypeId).MaxCount;

        if (max != null)
            return (float)comp.Count / max.Value;

        return comp.Count != 0f ? 1f - 1f / (comp.Count + 2f) : 0f;
    }
}
