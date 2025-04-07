using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks if the entire list of keys exists
/// </summary>
public sealed partial class KeysExistsPrecondition : HTNPrecondition
{
    [DataField]
    public List<string> Keys = new();

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        foreach (var key in Keys)
        {
            var contains = blackboard.ContainsKey(key);
            if (!contains && !Invert || contains && Invert)
                return false;
        }

        return true;
    }
}
