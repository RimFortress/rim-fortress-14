using Content.Server._RF.NPC.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._RF.NPC;
using Content.Shared.Item;

namespace Content.Server._RF.NPC.HTN.Operators;

public sealed partial class MarkOwnerInRangeOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private EntityLookupSystem _lookup;
    private OwnedSystem _owned;

    [DataField]
    public float Range = 1f;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _owned = sysManager.GetEntitySystem<OwnedSystem>();
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entityManager.TryGetComponent(owner, out ControllableNpcComponent? control))
            return;

        var coords = _entityManager.GetComponent<TransformComponent>(owner).Coordinates;
        var entities = _lookup.GetEntitiesInRange<ItemComponent>(coords, Range);

        foreach (var entity in entities)
        {
            _owned.AddOwners(entity, control.CanControl);
        }
    }
}
