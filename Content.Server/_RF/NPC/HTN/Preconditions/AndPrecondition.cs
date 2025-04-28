using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Combines multiple preconditions into a logical AND
/// </summary>
public sealed partial class AndPrecondition : HTNPrecondition
{
    [DataField]
    public List<HTNPrecondition> Preconditions = new();

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        foreach (var precondition in Preconditions)
        {
            precondition.Initialize(sysManager);
        }
    }

    public override bool IsMet(NPCBlackboard blackboard)
    {
        foreach (var precondition in Preconditions)
        {
            if (!precondition.IsMet(blackboard))
                return false;
        }

        return true;
    }
}
