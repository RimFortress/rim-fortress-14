using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Robust.Shared.Random;

namespace Content.Server._RF.NPC.GOAP.Conditions;

/// <summary>
/// A condition which is satisfied with a random chance.
/// </summary>
public sealed partial class Random : BaseGoapCondition<Random>
{
    /// <summary>
    /// Chance of success from 0 to 1.
    /// </summary>
    [DataField]
    public float Chance;
}

public sealed class RandomGoapConditionSystem : GoapConditionSystem<Random>
{
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, Random condition)
        => _random.NextFloat() < condition.Chance;
}
