using System.Linq;
using Content.Server._RF.NPC.Prototypes;
using Content.Server._RF.NPC.Systems;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.NPC.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class NpcCommand : ToolshedCommand
{
    private NpcControlSystem? _npcControl;

    [CommandImplementation("set_passive_task")]
    public IEnumerable<EntityUid> SetPassiveTask(
        [PipedArgument] IEnumerable<EntityUid> npcUid,
        ProtoId<NpcTaskPrototype> task)
    {
        _npcControl ??= GetSys<NpcControlSystem>();
        return npcUid.Where(uid => _npcControl.TrySetPassiveTask(uid, task));
    }

    [CommandImplementation("set_task")]
    public IEnumerable<EntityUid> SetTask(
        [PipedArgument] IEnumerable<EntityUid> npcUid,
        ProtoId<NpcTaskPrototype> task,
        EntityUid target)
    {
        _npcControl ??= GetSys<NpcControlSystem>();
        return npcUid.Where(uid => _npcControl.TrySetTask(uid, task, target));
    }

    [CommandImplementation("add_allowed_task")]
    public IEnumerable<EntityUid> AddAllowedTask(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<NpcTaskPrototype> task)
    {
        _npcControl ??= GetSys<NpcControlSystem>();

        return uids.Where(uid =>
        {
            _npcControl.AddAllowedTask(uid, task);
            return true;
        });
    }

    [CommandImplementation("remove_allowed_task")]
    public IEnumerable<EntityUid> RemoveAllowedTask(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<NpcTaskPrototype> task)
    {
        _npcControl ??= GetSys<NpcControlSystem>();

        return uids.Where(uid =>
        {
            _npcControl.RemoveAllowedTask(uid, task);
            return true;
        });
    }

    [CommandImplementation("add_control")]
    public IEnumerable<EntityUid> AddControl(
        [PipedArgument] IEnumerable<EntityUid> uids,
        EntityUid controller)
    {
        _npcControl ??= GetSys<NpcControlSystem>();

        return uids.Where(uid =>
        {
            _npcControl.AddNpcControl(controller, uid);
            return true;
        });
    }
}
