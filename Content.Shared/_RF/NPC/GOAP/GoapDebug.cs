using Content.Shared._RF.NPC.GOAP.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._RF.NPC.GOAP;

public interface IGoapDebuggable
{
    GoapDebugDump? Dump { get; set; }
}

/// <summary>
/// Dump with a debug message from some GOAP class.
/// </summary>
/// <param name="Dump">String with a debug message from the system.</param>
/// <param name="StateSnapshot">Debug snapshot of the GOAP of the current agent state.</param>
[Serializable, NetSerializable]
public readonly record struct GoapDebugDump(
    string? Dump,
    GoapStateDebugDump StateSnapshot);

/// <summary>
/// Debug GoapState dump.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct GoapStateDebugDump(
    Dictionary<string, (string Type, string Value)> State);

/// <summary>
/// A single step in the A* search: trying to apply a task to a state.
/// </summary>
/// <param name="TaskId">Index of this node in the list of all nodes available for planning.</param>
/// <param name="Compound">The compound from which this node was extracted.</param>
/// <param name="Preconditions">Dump about the conditions of the task.</param>
/// <param name="StateBefore">State before applying the task.</param>
/// <param name="StateAfter">State after applying effects (if preconditions met).</param>
/// <param name="TaskCost">Task cost (sum of action costs).</param>
/// <param name="Heuristic">Heuristic value for the resulting state.</param>
/// <param name="AddedToOpenList">Was this node added to the open set?</param>
/// <param name="PreconditionsMet">Were preconditions satisfied?</param>
/// <param name="SkipReason">Reason why node was skipped (if not added).</param>
[Serializable, NetSerializable]
public readonly record struct GoapNodeDebugEntry(
    int TaskId,
    ProtoId<GoapCompoundPrototype>? Compound,
    GoapPreconditionDebugDump[] Preconditions,
    GoapStateDebugDump StateBefore,
    GoapStateDebugDump? StateAfter,
    float TaskCost,
    float Heuristic,
    bool AddedToOpenList,
    bool PreconditionsMet,
    string? SkipReason);

[Serializable, NetSerializable]
public readonly record struct GoapPreconditionDebugDump(GoapDebugDump Dump, bool Result);

/// <summary>
/// Debug information about the planning process for a single GOAP agent.
/// </summary>
/// <param name="StartState">Agent state before planning.</param>
/// <param name="GoalState">Desired goal state.</param>
/// <param name="TotalCost">Total cost of the found plan.</param>
/// <param name="Success">Whether a plan was found.</param>
/// <param name="NodesExpanded">Expanded node count.</param>
/// <param name="ElapsedTime">Planning elapsed time.</param>
/// <param name="Nodes">Step-by-step log of node expansions.</param>
/// <param name="Actions">Debug information for each action in the plan.</param>
[Serializable, NetSerializable]
public record struct GoapPlanDebugInfo(
    GoapStateDebugDump StartState,
    GoapStateDebugDump GoalState,
    float TotalCost,
    bool Success,
    int NodesExpanded,
    TimeSpan ElapsedTime,
    List<GoapNodeDebugEntry> Nodes,
    List<GoapActionDebugInfo> Actions);

/// <summary>
/// Debug information about the GOAP plan action.
/// </summary>
/// <param name="NodeIndex">The index of the planning node to which the action belonged.</param>
/// <param name="ActionIndex">Action index in the planning node.</param>
/// <param name="StartupSuccess">Was the action start successful?</param>
/// <param name="StartupDump">Dump about action startup.</param>
/// <param name="ShutdownDump">Dump about action shutdown.</param>
/// <param name="UpdateDumps">Dumps about action updates.</param>
[Serializable, NetSerializable]
public readonly record struct GoapActionDebugInfo(
    int NodeIndex,
    int ActionIndex,
    bool? StartupSuccess,
    GoapDebugDump? StartupDump,
    GoapDebugDump? ShutdownDump,
    List<GoapActionUpdateDebugDump> UpdateDumps)
{
    public GoapActionDebugInfo WithUpdate(GoapActionUpdateDebugDump update)
        => this with { UpdateDumps = new(UpdateDumps) { update } };

    public GoapActionDebugInfo WithStartup(bool success, GoapDebugDump? dump)
        => this with { StartupSuccess = success, StartupDump = dump };

    public GoapActionDebugInfo WithShutdown(GoapDebugDump? dump)
        => this with { ShutdownDump = dump };
}

[Serializable, NetSerializable]
public readonly record struct GoapActionUpdateDebugDump(
    GameTick Tick,
    GoapDebugDump Dump,
    GoapActionResult Result);

// Static graph stuff

/// <summary>
/// Represents a static dependency graph of GOAP tasks.
/// Nodes are executable tasks, and edges represent that
/// one task's effects can satisfy another task's preconditions.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct GoapStaticGraph(
    List<GoapStaticGraphNode> Nodes,
    List<GoapStaticGraphEdge> Edges,
    Dictionary<int, List<GoapStaticGraphEdge>> OutgoingByNodeId,
    Dictionary<int, List<GoapStaticGraphEdge>> IncomingByNodeId);

/// <summary>
/// Represents a single GOAP task node in the static graph.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct GoapStaticGraphNode(
    int Id,
    List<GoapStaticGraphObject> Actions,
    List<GoapStaticGraphObject> Preconditions,
    GoapStateDebugDump EffectsDump);

/// <summary>
/// Represents a directed edge between two GOAP tasks.
/// The edge exists if the source task's effects satisfy
/// one of the destination task's preconditions.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct GoapStaticGraphEdge(
    int FromNodeId,
    int ToNodeId,
    int ConditionIndex,
    string ConditionType);

[Serializable, NetSerializable]
public readonly record struct GoapStaticGraphObject(
    string Type,
    Dictionary<string, (string Type, string Value)> Reflection);

// Breakpoints

[Serializable, NetSerializable]
public enum GoapBreakpointKind : byte
{
    Precondition,
    ActionStartup,
    ActionUpdate,
    ActionShutdown,
}

[Serializable, NetSerializable]
public enum GoapBreakpointResultKind : byte
{
    /// <summary>
    /// <see cref="GoapActionResult.Finished"/>
    /// </summary>
    Finished,

    /// <summary>
    /// <see cref="GoapActionResult.Continuing"/>
    /// </summary>
    Continuing,

    /// <summary>
    /// <see cref="GoapActionResult.Failed"/>
    /// </summary>
    Failed,

    True,
    False,
    None,
}

/// <summary>
/// A class representing a custom breakpoint for a GOAP event.
/// When this breakpoint is triggered, the client receives debugging information about the plan.
/// </summary>
/// <param name="Target">Target GOAP NPC.</param>
/// <param name="NodeId">The ID of the node in the plan containing the target goap object.</param>
/// <param name="Index">The object's index in the node.</param>
/// <param name="Kind">The GOAP type of the object tracked by this breakpoint.</param>
/// <param name="Result">The result of the object under which the client will receive debugging information.</param>
[Serializable, NetSerializable]
public readonly record struct GoapBreakpoint(
    NetEntity Target,
    int NodeId,
    int Index,
    GoapBreakpointKind Kind,
    GoapBreakpointResultKind Result);
