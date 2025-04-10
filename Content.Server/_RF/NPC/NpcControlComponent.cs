using Content.Shared._RF.NPC;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC;

[RegisterComponent]
public sealed partial class NpcControlComponent : SharedNpcControlComponent
{
    /// <summary>
    /// Tasks that this entity can issue
    /// </summary>
    [DataField]
    public List<ProtoId<NpcTaskPrototype>> Tasks = new();
}
