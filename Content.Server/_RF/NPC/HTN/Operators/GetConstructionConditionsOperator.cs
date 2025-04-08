using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.Construction.Conditions;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Construction;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Obtains and stores the necessary conditions to execute the current construction edge
/// </summary>
public sealed partial class GetConstructionConditionsOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private ConstructionSystem _construction = default!;

    /// <summary>
    /// The structure whose conditions are to be received
    /// </summary>
    [DataField]
    public string TargetKey = "Target";

    /// <summary>
    /// Material needed to build the structure
    /// </summary>
    [DataField]
    public string MaterialInsertKey = "MaterialTarget";

    /// <summary>
    /// Amount of material to build the structure
    /// </summary>
    [DataField]
    public string MaterialInsertAmountKey = "MaterialInsertAmount";

    /// <summary>
    /// Entities with these tags are needed for construction
    /// </summary>
    [DataField]
    public string TagsKey = "TagsTarget";

    /// <summary>
    /// Does an entity have to have all tags to build or just one tag
    /// </summary>
    [DataField]
    public string AllTagsKey = "AllTagsRequired";

    /// <summary>
    /// Type of tool needed for construction
    /// </summary>
    [DataField]
    public string ToolQualityKey = "ToolQualityTarget";

    /// <summary>
    /// The amount of fuel to be consumed for construction
    /// </summary>
    [DataField]
    public string ToolFuelKey = "ToolFuelTarget";

    /// <summary>
    /// A component that an entity must have
    /// </summary>
    [DataField]
    public string ComponentKey = "ComponentTarget";

    [ValidatePrototypeId<ToolQualityPrototype>]
    private readonly ProtoId<ToolQualityPrototype> _anchoringQuality = "Anchoring";

    [ValidatePrototypeId<ToolQualityPrototype>]
    private readonly ProtoId<ToolQualityPrototype> _weldingQuality = "Welding";

    [ValidatePrototypeId<ToolQualityPrototype>]
    private readonly ProtoId<ToolQualityPrototype> _screwingQuality = "Screwing";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _construction = sysManager.GetEntitySystem<ConstructionSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid uid, _entManager)
            || !_entManager.TryGetComponent(uid, out ConstructionComponent? construction))
            return (false, null);

        var (node, edge) = _construction.GetCurrentNodeAndEdge(uid, construction);

        if (node == null || edge == null
            && (construction.NodePathfinding == null || construction.NodePathfinding.Count == 0))
            return (false, null);

        edge ??= node.GetEdge(construction.NodePathfinding!.ToList().First());

        if (edge == null)
            return (false, null);

        var materials = new List<string>();
        var materialAmounts = new List<int>();
        var tags = new List<List<string>>();
        var allTags = new List<bool>();
        var toolQualities = new List<string>();
        var toolFuels = new List<float>();
        var components = new List<string>();

        var conditions = new Queue<IGraphCondition>(edge.Conditions);
        while (conditions.TryDequeue(out var condition))
        {
            if (condition.Condition(uid, _entManager))
                continue;

            switch (condition)
            {
                case AllConditions all:
                    foreach (var cond in all.Conditions)
                    {
                        conditions.Enqueue(cond);
                    }
                    break;
                case AnyConditions any:
                    foreach (var cond in any.Conditions)
                    {
                        // Take the first supported condition
                        if (cond is not (AllConditions
                            or AnyConditions
                            or EntityAnchored
                            or DoorWelded
                            or StorageWelded
                            or WirePanel
                            or HasTag
                            or MachineFrameComplete))
                            continue;

                        conditions.Enqueue(cond);
                        break;
                    }
                    break;
                case EntityAnchored:
                    toolQualities.Add(_anchoringQuality);
                    toolFuels.Add(0);
                    break;
                case DoorWelded:
                case StorageWelded:
                    toolQualities.Add(_weldingQuality);
                    toolFuels.Add(10f);
                    break;
                case WirePanel:
                    toolQualities.Add(_screwingQuality);
                    toolFuels.Add(0);
                    break;
                case HasTag tag:
                    tags.Add(new() { tag.Tag });
                    allTags.Add(true);
                    break;
                case MachineFrameComplete:
                    if (!_entManager.TryGetComponent(uid, out MachineFrameComponent? frame))
                        break;

                    foreach (var (type, amount) in frame.MaterialRequirements)
                    {
                        materials.Add(type);
                        materialAmounts.Add(amount);
                    }

                    foreach (var (compName, _) in frame.ComponentRequirements)
                    {
                        components.Add(compName);
                    }

                    foreach (var (tagName, _) in frame.TagRequirements)
                    {
                        tags.Add(new() { tagName });
                        allTags.Add(true);
                    }
                    break;
            }
        }

        var effects = new Dictionary<string, object>();

        if (materials.Count != 0)
            effects.Add(MaterialInsertKey, materials);
        if (materialAmounts.Count != 0)
            effects.Add(MaterialInsertAmountKey, materialAmounts);
        if (tags.Count != 0)
            effects.Add(TagsKey, tags);
        if (allTags.Count != 0)
            effects.Add(AllTagsKey, allTags);
        if (toolQualities.Count != 0)
            effects.Add(ToolQualityKey, toolQualities);
        if (toolFuels.Count != 0)
            effects.Add(ToolFuelKey, toolFuels);
        if (components.Count != 0)
            effects.Add(ComponentKey, components);

        return effects.Count == 0 ? (false, null) : (true, effects);
    }
}
