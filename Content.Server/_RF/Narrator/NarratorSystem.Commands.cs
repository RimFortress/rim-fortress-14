using Content.Server.Administration;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._RF.Narrator;

public partial class NarratorSystem
{
    private void InitializeCommands()
    {
        _host.RegisterCommand("narratorinfo",
            Loc.GetString("cmd-narratorinfo-decs"),
            Loc.GetString("cmd-narratorinfo-help"),
            NarratorInfoCallback,
            NarratorInfoCallbackHelper);
    }

    [AdminCommand(AdminFlags.Debug)]
    private void NarratorInfoCallback(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var player = _player.LocalEntity;

        if (args.Length == 1 && _player.TryGetSessionByUsername(args[0], out var session))
            player = session.AttachedEntity;

        if (player == null)
            return;

        var text = "";
        var enumerator = EntityQueryEnumerator<RimFortressRuleComponent>();

        while (enumerator.MoveNext(out var comp))
        {
            text += DebugText(player.Value, comp.Narrator);
        }

        shell.WriteLine(text);
    }

    private CompletionResult NarratorInfoCallbackHelper(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "Player")
            : CompletionResult.Empty;
    }
}
