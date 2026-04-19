using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._RF.NPC.GOAP;

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
/// <param name="Preconditions">Dump about the conditions of the task.</param>
/// <param name="Effects">Effects of completing this task.</param>
/// <param name="StateBefore">State before applying the task.</param>
/// <param name="StateAfter">State after applying effects (if preconditions met).</param>
/// <param name="TaskCost">Task cost (sum of action costs).</param>
/// <param name="Heuristic">Heuristic value for the resulting state.</param>
/// <param name="AddedToOpanList">Was this node added to the open set?</param>
/// <param name="PreconditionsMet">Were preconditions satisfied?</param>
/// <param name="SkipReason">Reason why node was skipped (if not added).</param>
[Serializable, NetSerializable]
public readonly record struct GoapNodeDebugEntry(
    GoapPreconditionDebugDump[] Preconditions,
    GoapStateDebugDump Effects,
    GoapStateDebugDump StateBefore,
    GoapStateDebugDump? StateAfter,
    float TaskCost,
    float Heuristic,
    bool AddedToOpanList,
    bool PreconditionsMet,
    string? SkipReason);

[Serializable, NetSerializable]
public readonly record struct GoapPreconditionDebugDump(
    string Type,
    GoapDebugDump Dump,
    bool Result);

/// <summary>
/// Debug information about the planning process for a single GOAP agent.
/// </summary>
/// <param name="StartState">Start state before planning.</param>
/// <param name="GoalState">Desired goal state.</param>
/// <param name="NodesExpanded">Total nodes expanded during A*.</param>
/// <param name="TotalCost">Total cost of the found plan.</param>
/// <param name="Success">Whether a plan was found.</param>
/// <param name="Nodes">Step-by-step log of node expansions.</param>
[Serializable, NetSerializable]
public record struct GoapPlanDebugInfo(
    GoapStateDebugDump StartState,
    GoapStateDebugDump GoalState,
    int NodesExpanded,
    float TotalCost,
    bool Success,
    List<GoapNodeDebugEntry> Nodes);

/// <summary>
/// Debug information about the GOAP plan action.
/// </summary>
/// <param name="Type">GOAP action type.</param>
/// <param name="StartupDump">Dump about action startup.</param>
/// <param name="ShutdownDump">Dump about action shutdown.</param>
/// <param name="UpdateDumps">Dumps about action updates.</param>
[Serializable, NetSerializable]
public record struct GoapActionDebugInfo(
    string Type,
    GoapDebugDump? StartupDump,
    GoapDebugDump? ShutdownDump,
    List<GoapActionUpdateDebugDump> UpdateDumps);

[Serializable, NetSerializable]
public readonly record struct GoapActionUpdateDebugDump(
    GameTick Tick,
    GoapDebugDump Dump,
    GoapActionResult Result);