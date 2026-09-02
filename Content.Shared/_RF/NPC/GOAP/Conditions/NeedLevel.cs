using Content.Shared._RF.Needs.Prototypes;
using Content.Shared._RF.Needs.Systems;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.GOAP.Conditions;

/// <summary>
/// Checks the agent's need level.
/// </summary>
public sealed partial class NeedLevel : BaseGoapCondition<NeedLevel>
{
    /// <summary>
    /// Need prototype.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NeedPrototype> Need;

    /// <summary>
    /// The minimum required need value.
    /// </summary>
    [DataField]
    public StateKey<float>? MinKey;

    /// <summary>
    /// The maximum required need value.
    /// </summary>
    [DataField]
    public StateKey<float>? MaxKey;
}

public sealed partial class NeedLevelSystem : GoapConditionSystem<NeedLevel>
{
    [Dependency] private NeedsSystem _needs = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, NeedLevel condition)
    {
        if (condition.MinKey == null && condition.MaxKey == null)
        {
            CreateDump("warn: MinKey and MaxKey is null");
            return true;
        }

        var level = _needs.GetValue(uid, condition.Need);
        CreateDump($"'{condition.Need}' level == {level}");

        if (condition.MinKey is { } minKey)
        {
            if (!TryGet(state, minKey, out var min) || level < min)
                return false;
        }

        if (condition.MaxKey is { } maxKey)
        {
            if (!TryGet(state, maxKey, out var max) || level > max)
                return false;
        }

        return true;
    }
}
