using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Combines multiple preconditions into a logical OR
/// </summary>
public sealed partial class OrPrecondition : HTNPrecondition
{
    [DataField]
    public List<HTNPrecondition> Preconditions = new();

    public override bool IsMet(NPCBlackboard blackboard)
    {
        foreach (var precondition in Preconditions)
        {
            if (precondition.IsMet(blackboard))
                return true;
        }

        return false;
    }
}
