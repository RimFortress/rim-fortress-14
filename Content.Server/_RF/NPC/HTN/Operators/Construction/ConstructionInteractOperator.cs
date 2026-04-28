using Content.Server._RF.Construction;
using Content.Server.Hands.Systems;
using Content.Server.Interaction;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.Storage.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Timing;
using Content.Shared.Tools.Components;

namespace Content.Server._RF.NPC.HTN.Operators.Construction;

public sealed partial class ConstructionInteractOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private UseDelaySystem _useDelay = default!;
    private SharedDoAfterSystem _doAfter = default!;
    private SharedCombatModeSystem _combatMode = default!;
    private InventorySystem _inventory = default!;
    private InteractionSystem _interaction = default!;
    private HandsSystem _hands = default!;
    private StorageSystem _storage = default!;
    private NpcConstructionSystem _construction = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _useDelay = sysManager.GetEntitySystem<UseDelaySystem>();
        _doAfter = sysManager.GetEntitySystem<SharedDoAfterSystem>();
        _combatMode = sysManager.GetEntitySystem<SharedCombatModeSystem>();
        _inventory = sysManager.GetEntitySystem<InventorySystem>();
        _interaction = sysManager.GetEntitySystem<InteractionSystem>();
        _hands = sysManager.GetEntitySystem<HandsSystem>();
        _storage = sysManager.GetEntitySystem<StorageSystem>();
        _construction = sysManager.GetEntitySystem<NpcConstructionSystem>();
    }

    /// <summary>
    /// Key that contains the target entity.
    /// </summary>
    [DataField(required: true)]
    public string TargetKey = default!;

    public readonly string CurrentDoAfter = "CurrentConstructionInteractDoAfter";

    // Ensure that CurrentDoAfter doesn't exist as we enter this operator,
    // the code currently relies on the result of a TryGetValue
    public override void Startup(NPCBlackboard blackboard)
    {
        blackboard.Remove<ushort>(CurrentDoAfter);
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        blackboard.Remove<ushort>(CurrentDoAfter);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entity))
            return HTNOperatorStatus.Failed;

        if (_entity.HasComponent<ActiveDoAfterComponent>(owner))
            return HTNOperatorStatus.Continuing;

        // Handle ongoing doAfter, and store the doAfter.nextId so we can detect if we started one
        ushort nextId = 0;
        if (_entity.TryGetComponent<DoAfterComponent>(owner, out var doAfter))
        {
            // if CurrentDoAfter contains something, we have an active doAfter
            if (blackboard.TryGetValue<ushort>(CurrentDoAfter, out var doAfterId, _entity))
            {
                return _doAfter.GetStatus(owner, doAfterId) switch
                {
                    DoAfterStatus.Running => HTNOperatorStatus.Continuing,
                    DoAfterStatus.Finished => HTNOperatorStatus.Finished,
                    _ => HTNOperatorStatus.Failed,
                };
            }

            nextId = doAfter.NextId;
        }

        if (_entity.TryGetComponent<UseDelayComponent>(owner, out var useDelay)
            && _useDelay.IsDelayed(new(owner, useDelay)))
            return HTNOperatorStatus.Continuing;

        if (_entity.TryGetComponent<CombatModeComponent>(owner, out var combatMode))
            _combatMode.SetInCombatMode(owner, false, combatMode);

        if (!_construction.TryGetNextItem(target, owner, out var item, out var reason))
            return HTNOperatorStatus.Failed;

        // If we have an item in hands, we put it away in inventory
        if (_hands.TryGetActiveItem(owner, out var handItem) && handItem != item)
        {
            // If the welder is turned on in hands, turn it off first
            if (_entity.TryGetComponent(handItem, out WelderComponent? welder)
                && _entity.TryGetComponent(handItem, out TransformComponent? itemForm)
                && welder.Enabled)
                _interaction.UserInteraction(owner, itemForm.Coordinates, handItem);

            foreach (var entity in _inventory.GetHandOrInventoryEntities(owner))
            {
                if (_entity.TryGetComponent(entity, out StorageComponent? storage)
                    && _storage.Insert(entity, handItem.Value, out _, storageComp: storage))
                    break;
            }

            // If we couldn't put the item in the inventory, we throw it away
            if (_hands.TryGetActiveItem(owner, out _))
            {
                if (!_hands.TryDrop(handItem.Value))
                    return HTNOperatorStatus.Failed;
            }
        }

        // Pick up the item
        if (handItem != item && !_hands.TryPickup(owner, item.Value))
            return HTNOperatorStatus.Failed;

        // Turn on welder
        if (_entity.TryGetComponent(handItem, out WelderComponent? nextWelder)
            && _entity.TryGetComponent(handItem, out TransformComponent? nextForm)
            && !nextWelder.Enabled)
            _interaction.UserInteraction(owner, nextForm.Coordinates, handItem);

        // Start construction
        _interaction.UserInteraction(owner, _entity.GetComponent<TransformComponent>(target).Coordinates, target);

        // Detect doAfter, save it, and don't exit from this operator
        if (doAfter != null && nextId != doAfter.NextId)
        {
            blackboard.SetValue(CurrentDoAfter, nextId);
            return HTNOperatorStatus.Continuing;
        }

        return HTNOperatorStatus.Finished;
    }
}
