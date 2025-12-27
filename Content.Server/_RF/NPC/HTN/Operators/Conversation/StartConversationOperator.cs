using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Conversation;
using Content.Server._RF.NPC.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._RF.Conversation;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RF.NPC.HTN.Operators.Conversation;

/// <summary>
/// Starts a conversation with random matching entities
/// </summary>
public sealed partial class StartConversationOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private ConversationSystem _conversation = default!;
    private TransformSystem _xform = default!;
    private EntityLookupSystem _lookup = default!;

    /// <summary>
    /// A prototype of the conversation script that will be started.
    /// If null, the script will be selected randomly from the matching ones
    /// </summary>
    [DataField]
    public ProtoId<ConversationScriptPrototype>? Script;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _conversation = sysManager.GetEntitySystem<ConversationSystem>();
        _xform = sysManager.GetEntitySystem<TransformSystem>();
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entity.TryGetComponent(owner, out ControllableNpcComponent? controllable))
            return (false, null);

        var radius = blackboard.GetValueOrDefault<float>(blackboard.GetVisionRadiusKey(_entity), _entity);
        var ownerCoords = _xform.GetMapCoordinates(owner);
        var entities = new List<EntityUid>();
        var canControl = controllable.CanControl.ToList();

        foreach (var ent in _lookup.GetEntitiesInRange<ControllableNpcComponent>(ownerCoords, radius))
        {
            if (ent.Comp.CanControl.Any(x => canControl.Contains(x)))
                entities.Add(ent);
        }

        _prototype.TryIndex(Script, out var script);

        if (script == null)
        {
            var prototype = _prototype
                .EnumeratePrototypes<ConversationScriptPrototype>()
                .ToList();

            while (prototype.Count > 0)
            {
                var proto = _random.PickAndTake(prototype);

                if (_conversation.TryStartConversation(proto, entities))
                    return (true, null);
            }
        }
        else if (!_conversation.TryStartConversation(script, entities))
            return (false, null);

        return (false, null);
    }
}
