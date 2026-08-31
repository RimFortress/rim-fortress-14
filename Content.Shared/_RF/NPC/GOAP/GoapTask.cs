using Content.Shared._RF.NPC.GOAP.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// An abstract action plan node. GOAP has no knowledge of what
/// happens inside it and requires explicit state transitions affected by the actions.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class GoapTask
{
    /// <summary>
    /// Preconditions required to start this node.
    /// </summary>
    [DataField]
    public List<GoapCondition> Preconditions = new();

    /// <summary>
    /// Effects that will be applied to the agent's state after this node completes.
    /// </summary>
    [DataField]
    public GoapState Effects = new();
}

/// <summary>
/// A single action with conditions and effects.
/// </summary>
public sealed partial class GoapActionTask : GoapTask
{
    [DataField(required: true)]
    public GoapAction Action = default!;
}

/// <summary>
/// A set of actions grouped into a single node with shared conditions and effects.
/// </summary>
/// <remarks>
/// Use this to group overly atomized actions into a single,
/// unified action to make it easier to write AI code and for the planner to work.
/// For example, instead of specifying conditions and effects for actions such as
/// switching to the free hand, picking up an item, and placing the item in the inventory,
/// combine these into a single set that performs the action of picking up the item.
/// </remarks>
public sealed partial class GoapCompoundTask : GoapTask
{
    [DataField(required: true)]
    public List<GoapAction> Actions = new();
}

/// <summary>
/// Includes a set of tasks from the prototype.
/// This task itself does nothing; it simply replaces the set of tasks.
/// </summary>
public sealed partial class GoapCompoundPrototypeTask : GoapTask
{
    [DataField(required: true)]
    public ProtoId<GoapCompoundPrototype> Proto;
}
