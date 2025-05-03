using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks if the entire list of keys exists
/// </summary>
public sealed partial class KeysExistsPrecondition : InvertiblePrecondition
{
    [DataField]
    public List<string> Keys = new();

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        foreach (var key in Keys)
        {
            if (!blackboard.ContainsKey(key))
                return false;
        }

        return true;
    }
}
