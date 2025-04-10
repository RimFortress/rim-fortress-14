using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks if the key with the entity is valid
/// </summary>
public sealed partial class EntityDeletedPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string Key = default!;

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(Key, out EntityUid target, _entManager)
            || !_entManager.TryGetComponent(target, out MetaDataComponent? meta)
            || meta.EntityDeleted)
            return !Invert;

        return Invert;
    }
}
