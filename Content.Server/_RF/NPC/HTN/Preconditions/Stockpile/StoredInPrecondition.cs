using Content.Server.NPC;
using Content.Shared._RF.Stockpile;

namespace Content.Server._RF.NPC.HTN.Preconditions.Stockpile;

/// <summary>
/// Checks the stockpile where the target entity is located
/// </summary>
public sealed partial class StoredInPrecondition : InvertiblePrecondition
{
    [DataField(required: true)]
    public string TargetKey;

    [DataField(required: true)]
    public string TargetStockKey;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid uid, EntityManager)
               && blackboard.TryGetValue(TargetStockKey, out Stock? stockpile, EntityManager)
               && stockpile.ContainsEntity(uid);
    }
}
