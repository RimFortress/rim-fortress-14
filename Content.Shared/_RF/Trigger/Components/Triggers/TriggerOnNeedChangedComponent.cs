using Content.Shared._RF.Needs;
using Content.Shared.Trigger.Components.Triggers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Trigger.Components.Triggers;

/// <summary>
/// Triggered when the threshold of need changes
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TriggerOnNeedChangedComponent : BaseTriggerOnXComponent
{
    [DataField(required: true)]
    public ProtoId<NeedPrototype> Need;

    [DataField]
    public string? Threshold;
}
