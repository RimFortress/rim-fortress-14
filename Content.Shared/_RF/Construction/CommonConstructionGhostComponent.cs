using Content.Shared.Construction.Prototypes;

namespace Content.Shared._RF.Construction;

/// <summary>
/// Construction ghost available on the server
/// </summary>
[RegisterComponent]
public sealed partial class CommonConstructionGhostComponent : Component
{
    /// <summary>
    /// The construction prototype of this ghost
    /// </summary>
    [ViewVariables]
    public ConstructionPrototype Prototype { get; set; }
}
