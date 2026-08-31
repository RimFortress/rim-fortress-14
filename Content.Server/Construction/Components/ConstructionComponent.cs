using Content.Shared.Construction.Prototypes;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;

using Content.Server._RF.Construction; // RimFortress

namespace Content.Server.Construction.Components
{
    [RegisterComponent, Access(typeof(ConstructionSystem), typeof(CommonConstructionSystem))] // RimFortress: Access
    public sealed partial class ConstructionComponent : Component
    {
        [DataField(required: true)]
        public ProtoId<ConstructionGraphPrototype> Graph;

        [DataField(required: true)]
        public string Node = string.Empty;

        [DataField("edge")]
        public int? EdgeIndex;

        [DataField("step")]
        public int StepIndex;

        [DataField]
        public HashSet<string> Containers = new();

        [DataField("defaultTarget")]
        public string? TargetNode;

        [ViewVariables]
        public int? TargetEdgeIndex;

        [ViewVariables]
        public Queue<string>? NodePathfinding;

        [DataField("deconstructionTarget")]
        public string? DeconstructionNode = "start";

        [ViewVariables]
        // TODO Force flush interaction queue before serializing to YAML.
        // Otherwise you can end up with entities stuck in invalid states (e.g., waiting for DoAfters).
        public readonly Queue<object> InteractionQueue = new();
    }
}
