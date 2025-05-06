using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// HTNPrecondition with integrated inversion functionality
/// </summary>
public abstract partial class InvertiblePrecondition : HTNPrecondition
{
    [Dependency] protected readonly IEntityManager EntityManager = default!;

    /// <summary>
    /// Whether the output data should be inverted or not
    /// </summary>
    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var isMet = IsMetInvertible(blackboard);

        if (Invert)
            return !isMet;

        return isMet;
    }

    /// <summary>
    /// Regular check of the condition, then the result of this method will be automatically inverted if necessary
    /// </summary>
    public abstract bool IsMetInvertible(NPCBlackboard blackboard);
}
