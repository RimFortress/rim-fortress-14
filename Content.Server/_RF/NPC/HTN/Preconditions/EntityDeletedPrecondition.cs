using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks if the key with the entity is invalid
/// </summary>
public sealed partial class EntityDeletedPrecondition : InvertiblePrecondition
{
    [DataField(required: true)]
    public string Key = default!;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return !blackboard.TryGetValue(Key, out EntityUid target, EntityManager)
               || !EntityManager.TryGetComponent(target, out MetaDataComponent? meta)
               || meta.EntityDeleted;
    }
}
