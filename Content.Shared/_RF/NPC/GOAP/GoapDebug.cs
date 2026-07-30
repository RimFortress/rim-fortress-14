using Content.Shared._RF.NPC.GOAP.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// An interface for GOAP objects able to store debugging information.
/// </summary>
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
/// <param name="NodeId">Index of this node in the list of all nodes available for planning.</param>
/// <param name="FromNodeId">The ID of the node from which the check was performed on this node.</param>
/// <param name="Preconditions">Dump about the conditions of the task.</param>
/// <param name="StateBefore">State before applying the task.</param>
/// <param name="StateAfter">State after applying effects (if preconditions met).</param>
/// <param name="TaskCost">Task cost (sum of action costs).</param>
/// <param name="PreconditionsMet">Were preconditions satisfied?</param>
/// <param name="InPlan">Is this node included in the final plan?</param>
/// <param name="HelpGoal">Whether the node's effects help with fully or partially achieving the goal.</param>
/// <param name="IndexInPlan">Node index in the plan.</param>
/// <param name="SkipReason">Reason why node was skipped (if not added).</param>
[Serializable, NetSerializable]
public readonly record struct GoapNodeDebugEntry(
    int NodeId,
    int? FromNodeId,
    GoapPreconditionDebugDump[] Preconditions,
    GoapStateDebugDump StateBefore,
    GoapStateDebugDump? StateAfter,
    float TaskCost,
    bool PreconditionsMet,
    bool InPlan,
    bool HelpGoal,
    int? IndexInPlan,
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
/// <param name="ConditionsChecked">Preconditions check count.</param>
/// <param name="EffectsApplied">Applied effects count.</param>
/// <param name="SkippedExpensiveNodes">Expensive node skip count.</param>
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
    int ConditionsChecked,
    int EffectsApplied,
    int SkippedExpensiveNodes,
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

    /// <summary>
    /// Returns all debug information about the action in text format.
    /// </summary>
    public string GetLogsString()
    {
        var str = $"NodeIndex: {NodeIndex}\nActionIndex: {ActionIndex}\n";

        if (StartupDump != null)
        {
            str += "Update:\n";
            str += $"  Result: {StartupSuccess}\n";

            if (StartupDump?.Dump is { } dump)
            {
                str += "  Logs:\n";

                foreach (var log in dump.Split('\n'))
                {
                    str += $"  - {log}\n";
                }
            }
        }

        if (ShutdownDump?.Dump != null)
        {
            str += "Shutdown:\n";

            foreach (var log in ShutdownDump.Value.Dump.Split('\n'))
            {
                str += $"- {log}\n";
            }
        }

        if (UpdateDumps.Count > 0)
        {
            str += "Updates:\n";
            str += $"  Result: {StartupSuccess}\n";
            str += "  Ticks:\n";

            foreach (var update in UpdateDumps)
            {
                str += $"  - Tick: {update.Tick}\n";
                str += $"    Result: {update.Result}\n";

                if (update.Dump.Dump is not { } dump)
                    continue;

                str += "    Logs:\n";

                foreach (var log in dump.Split('\n'))
                {
                    str += $"    - {log}\n";
                }
            }
        }

        return str;
    }
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
public readonly record struct GoapStaticGraphDebug(
    List<GoapStaticGraphNodeDebug> Nodes,
    List<GoapStaticGraphEdge> Edges,
    Dictionary<int, List<GoapStaticGraphEdge>> OutgoingByNodeId,
    Dictionary<int, List<GoapStaticGraphEdge>> IncomingByNodeId)
    : IStaticGraph<GoapStaticGraphNodeDebug, GoapStaticGraphEdge>;

/// <summary>
/// Represents a single GOAP task node in the static graph.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct GoapStaticGraphNodeDebug(
    int Id,
    List<ObjectDebugReflection> Actions,
    List<(ObjectDebugReflection Object, bool EntityCondition)> Preconditions,
    GoapStateDebugDump EffectsDump,
    ProtoId<GoapCompoundPrototype>? Compound) : IStaticGraphNode;

// Breakpoints

[Serializable, NetSerializable]
public enum GoapBreakpointKind : byte
{
    ActionStartup,
    ActionUpdate,
    ActionShutdown,
    Planning,
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
