using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Robust.Shared.Random;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// A condition that is triggered with a random chance
/// </summary>
public sealed partial class RandomPrecondition : HTNPrecondition
{
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Chance of the condition being triggered.
    /// From 0 to 1
    /// </summary>
    [DataField]
    public float Chance = 0.5f;

    public override bool IsMet(NPCBlackboard blackboard) => _random.Prob(Chance);
}
