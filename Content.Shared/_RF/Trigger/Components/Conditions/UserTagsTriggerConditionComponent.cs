using Content.Shared.Tag;
using Content.Shared.Trigger.Components.Conditions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Trigger.Components.Conditions;

/// <summary>
/// Checks user tags
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UserTagsTriggerConditionComponent : BaseTriggerConditionComponent
{
    [DataField]
    public List<ProtoId<TagPrototype>> Tags = new();

    [DataField]
    public bool RequireAll;

    [DataField]
    public bool Invert;
}
