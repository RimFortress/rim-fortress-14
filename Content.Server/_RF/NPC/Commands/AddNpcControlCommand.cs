using Content.Server._RF.NPC.Components;
using Content.Server.Administration;
using Content.Server.NPC.HTN;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._RF.NPC.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class AddNpcControlCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override string Command => "addnpccontrol";
    public override string Description => Loc.GetString("add-npc-control-command-description");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!int.TryParse(args[0], out var entInt) || !int.TryParse(args[1], out var targetInt))
        {
            shell.WriteLine(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        if (!_entityManager.TryGetEntity(new NetEntity(entInt), out var uid)
            || !_entityManager.TryGetEntity(new NetEntity(targetInt), out var targetUid))
        {
            shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
            return;
        }

        var npcControl = _entityManager.System<NpcControlSystem>();
        npcControl.AddNpcControl(uid.Value, targetUid.Value);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(CompletionHelper.Components<NpcControlComponent>(args[0]), "<user-uid>");

        if (args.Length == 2)
            return CompletionResult.FromHintOptions(CompletionHelper.Components<HTNComponent>(args[1]), "<target-uid>");

        return CompletionResult.Empty;
    }
}
