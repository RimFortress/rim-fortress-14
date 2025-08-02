using Content.Server.NPC;
using Content.Server.Storage.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions.Stockpile;

/// <summary>
/// Checks if the storage is closed
/// </summary>
public sealed partial class StorageClosedPrecondition : InvertiblePrecondition
{
    private EntityQuery<EntityStorageComponent> _storageQuery;

    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _storageQuery = EntityManager.GetEntityQuery<EntityStorageComponent>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
               && _storageQuery.TryComp(uid, out var comp)
               && !comp.Open;
    }
}
