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
}
