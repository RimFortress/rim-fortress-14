using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Construction;

[RegisterComponent, Access(typeof(SharedCommonConstructionSystem))]
public sealed partial class CommonConstructionGhostComponent : Component
{
    [DataField]
    public ProtoId<ConstructionPrototype> ConstructionProto;
}
