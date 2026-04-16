using Content.Shared._RF.Needs;
using Content.Shared._RF.Needs.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.EntityEffects;

/// <summary>
/// Changes the level of satisfaction of an entity's need
/// </summary>
public sealed partial class ChangeNeedEffect : EntityEffectBase<ChangeNeedEffect>
{
    /// <summary>
    /// Need prototype
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NeedPrototype> Need;

    /// <summary>
    /// How much will the value be increased
    /// </summary>
    [DataField]
    public float Amount;
}
