using System.Linq;
using Content.Server._RF.NPC.UtilityAi.Systems;
using Content.Server.Administration;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Content.Shared._RF.Toolshed;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.NPC.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class XUaiCommand : SystemCommand<ExecutableGoalSystem>
{
    [CommandImplementation("set_goal")]
    public IEnumerable<EntityUid> SetGoal(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<ExecutableGoalPrototype> goal,
        EntityUid target)
        => uids.Where(uid => System.TrySetGoal(uid, goal, target));

    [CommandImplementation("add_allowed_goal")]
    public IEnumerable<EntityUid> AddAllowedGoal(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<ExecutableGoalPrototype> goal)
        => uids.Where(uid =>
        {
            System.AddAllowedGoal(uid, goal);
            return true;
        });

    [CommandImplementation("remove_allowed_goal")]
    public IEnumerable<EntityUid> RemoveAllowedGoal(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<ExecutableGoalPrototype> goal)
        => uids.Where(uid =>
        {
            System.RemoveAllowedGoal(uid, goal);
            return true;
        });

    [CommandImplementation("add_control")]
    public IEnumerable<EntityUid> AddControl(
        [PipedArgument] IEnumerable<EntityUid> uids,
        EntityUid controller)
        => uids.Where(uid =>
        {
            System.AddControl(controller, uid);
            return true;
        });

    [CommandImplementation("remove_control")]
    public IEnumerable<EntityUid> RemoveControl(
        [PipedArgument] IEnumerable<EntityUid> uids,
        EntityUid controller)
        => uids.Where(uid => System.RemoveControl(controller, uid));
}

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class UaiCommand : SystemCommand<UtilityAiSystem>
{
    [CommandImplementation("get_score")]
    public IEnumerable<float> GetScore(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<UtilityAiGoalPrototype> goal)
        => uids.Select(uid => System.GetScore(uid, goal));

    [CommandImplementation("set_goal")]
    public IEnumerable<EntityUid> SetGoal(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<UtilityAiGoalPrototype> goal)
        => uids.Where(uid =>
        {
            System.SetGoal(uid, goal);
            return true;
        });
}
