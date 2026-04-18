using System.Threading;
using Content.Shared._RF.NPC.GOAP.Prototypes;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.GOAP.Components;

/// <summary>
/// A component used for Goal-Oriented Action Planning NPCs.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedGoapSystem))]
public sealed partial class GoapComponent : Component
{
    /// <inheritdoc cref="GoapState"/>
    [DataField]
    [Access(Other = AccessPermissions.ReadExecute)]
    public GoapState State = new();

    /// <summary>
    /// The target state that the NPC must achieve.
    /// </summary>
    [DataField]
    public GoapState GoalState = new();

    /// <inheritdoc cref="GoapCompoundPrototype"/>
    [DataField(required: true)]
    public ProtoId<GoapCompoundPrototype> RootTask;

    /// <inheritdoc cref="GoapPlan"/>
    [DataField]
    public GoapPlan? Plan;

    /// <summary>
    /// How long to wait after having planned to try planning again.
    /// </summary>
    [DataField]
    public TimeSpan PlanCooldown = TimeSpan.FromSeconds(0.45f);

    [DataField]
    public bool ConstantlyReplan = true;

    /// <summary>
    /// Is this NPC currently planning?
    /// </summary>
    [ViewVariables]
    public bool Planning => PlanningJob != null;

    /// <summary>
    /// Determines whether plans should be made / updated for this entity
    /// </summary>
    [DataField]
    public bool Enabled = true;

    /// <summary>
    /// A list of all actions available to NPCs.
    /// These actions will be used by the planner.
    /// </summary>
    [ViewVariables]
    public List<ExecutableGoapTask> ExecutableTasks = new();

    [ViewVariables]
    public GoapPlanJob? PlanningJob = null;

    [ViewVariables]
    public CancellationTokenSource? PlanningToken = null;

    /// <summary>
    /// When will the next planning attempt be made.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextPlanning;
}
