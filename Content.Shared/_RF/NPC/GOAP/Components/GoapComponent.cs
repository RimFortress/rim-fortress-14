using Content.Shared._RF.NPC.GOAP.Prototypes;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.GOAP.Components;

/// <summary>
/// A component used for Goal-Oriented Action Planning NPCs.
/// </summary>
[RegisterComponent]
[Access(typeof(GoapSystem))]
public sealed partial class GoapComponent : Component
{
    /// <inheritdoc cref="GoapState"/>
    [DataField]
    [Access(Other = AccessPermissions.ReadExecute)]
    public GoapState State = new();

    /// <summary>
    /// A list of all actions available to NPCs.
    /// These actions will be used by the planner.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<GoapCompoundPrototype> RootTask;

    /// <summary>
    /// List of remaining actions to be completed in the current plan.
    /// </summary>
    [DataField]
    public List<GoapAction> Plan = new();
}
