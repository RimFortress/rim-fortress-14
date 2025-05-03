using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks if the key with the entity is invalid
/// </summary>
public sealed partial class EntityDeletedPrecondition : InvertiblePrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string Key = default!;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return !blackboard.TryGetValue(Key, out EntityUid target, _entManager)
               || !_entManager.TryGetComponent(target, out MetaDataComponent? meta)
               || meta.EntityDeleted;
    }
}
