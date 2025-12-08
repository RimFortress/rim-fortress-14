using Content.Shared._RF.Socialization;
using Content.Shared.Trigger.Components.Conditions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Trigger.Components.Conditions;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HasOpinionEffectConditionComponent : BaseTriggerConditionComponent
{
    [DataField(required: true)]
    public ProtoId<SocializationEffectPrototype> Effect;

    [DataField]
    public bool Invert;
}
