using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Construction.Steps;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Obtains and stores the necessary steps to execute the current construction edge
/// </summary>
public sealed partial class GetConstructionStepsOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private ConstructionSystem _construction = default!;

    /// <summary>
    /// The structure whose requirements are to be received
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

        foreach (var step in edge.Steps)
        {
            switch (step)
            {
                case MaterialConstructionGraphStep insertMaterial:
                    materials.Add(insertMaterial.MaterialPrototypeId);
                    materialAmounts.Add(insertMaterial.Amount);
                    break;
                case TagConstructionGraphStep insertTag:
                    if (insertTag.Tag == null)
                        break;

                    tags.Add(new() { insertTag.Tag });
                    allTags.Add(true);
                    break;
                case MultipleTagsConstructionGraphStep insertMultipleTags:
                    if (insertMultipleTags.AllTag != null)
                    {
                        tags.Add(insertMultipleTags.AllTag.Select(t => (string)t).ToList());
                        allTags.Add(true);
                        break;
                    }

                    if (insertMultipleTags.AnyTag != null)
                    {
                        tags.Add(insertMultipleTags.AnyTag.Select(t => (string)t).ToList());
                        allTags.Add(false);
                    }

                    break;
                case ToolConstructionGraphStep insertTool:
                    toolQualities.Add(insertTool.Tool);
                    toolFuels.Add(insertTool.Fuel);
                    break;
                case ComponentConstructionGraphStep insertComponent:
                    components.Add(insertComponent.Component);
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
