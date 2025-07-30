using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Stockpile;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Server.Storage.Components;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC.HTN.Operators;

public sealed partial class FindStoringContainerOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private PathfindingSystem _pathfind = default!;
    private SharedTransformSystem _transform = default!;
    private StockpileSystem _stockpile = default!;

    /// <summary>
    /// When to shut the task down
    /// </summary>
    [DataField]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    /// <summary>
    /// Where the pathfinding result will be stored
    /// </summary>
    [DataField]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    /// <summary>
    /// The key of the entity for which you want to find a container in the stockpile
    /// </summary>
    [DataField(required: true)]
    public string TargetKey;

    /// <summary>
    /// Key in which the found container place will be stored
    /// </summary>
    [DataField(required: true)]
    public string ResultKey;

    /// <summary>
    /// How close we should try to build a path to the container
    /// </summary>
    [DataField]
    public string RangeKey = "MovementRange";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfind = sysManager.GetEntitySystem<PathfindingSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
        _stockpile = sysManager.GetEntitySystem<StockpileSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);

        if (_transform.GetGrid(owner) is not { } gridUid
            || !blackboard.TryGetValue(TargetKey, out EntityUid? uid, _entManager)
            || !_entManager.TryGetComponent(uid, out OwnedComponent? owned)
            || !_entManager.TryGetComponent(uid, out TransformComponent? xform))
            return (false, null);

        var containers = new List<(EntityUid Uid, float Dist)>();

        foreach (var stock in _stockpile.AllStockpiles())
        {
            if (!owned.Owners.Contains(stock.Owner)
                || stock.GridUid != gridUid
                || !_stockpile.CanInsert(stock, uid.Value))
                continue;

            foreach (var container in stock.Containers)
            {
                if (!_entManager.TryGetComponent(container, out EntityStorageComponent? storage)
                    || storage.Capacity <= storage.Contents.Count
                    || !_entManager.TryGetComponent(container, out TransformComponent? containerForm)
                    || !xform.Coordinates.TryDistance(_entManager, _transform, containerForm.Coordinates, out var distance))
                    continue;

                containers.Add((container, distance));
            }
        }

        containers.Sort((x, y) => x.Dist.CompareTo(y.Dist));

        foreach (var (ent, _) in containers)
        {
            var path = await _pathfind.GetPath(
                blackboard.GetValue<EntityUid>(NPCBlackboard.Owner),
                ent,
                range,
                cancelToken,
                _pathfind.GetFlags(blackboard));

            if (path.Result != PathResult.Path)
                continue;

            return (true, new()
            {
                { PathfindKey, path },
                { ResultKey, ent },
            });
        }

        return (false, null);
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        blackboard.Remove<PathResultEvent>(PathfindKey);
    }
}
