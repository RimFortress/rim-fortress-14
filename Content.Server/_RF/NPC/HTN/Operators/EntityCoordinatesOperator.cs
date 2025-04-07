using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Gets the coordinates of the entity
/// </summary>
public sealed partial class EntityCoordinatesOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string TargetKey = default!;

    [DataField]
    public string CoordinatesKey = "TargetCoordinates";

    [DataField]
    public bool Invert;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid? entity, _entManager)
            || !_entManager.TryGetComponent(entity, out TransformComponent? xform))
            return (false, null);

        return (true, new() { { CoordinatesKey, xform.Coordinates } });
    }
}
