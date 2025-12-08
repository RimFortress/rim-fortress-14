using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Trigger.Components;

/// <summary>
/// Component that allows different behaviors of a single component to be implemented when different keys are triggered
/// </summary>
[RegisterComponent]
public sealed partial class TriggerMappingsComponent : Component
{
    /// <summary>
    /// Components that will be given when certain keys are triggered
    /// </summary>
    [DataField]
    public Dictionary<string, ComponentRegistry> AddComponents = new();

    /// <summary>
    /// Will the components be overwritten by the given ones
    /// </summary>
    [DataField]
    public bool Override = true;
}
