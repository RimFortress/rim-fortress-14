using System.Linq;
using Content.Server.Administration;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RF.GameTicking.Rules;

public partial class RimFortressRuleSystem
{
    private void InitializeCommands()
    {
        _host.RegisterCommand("startworldrule",
            Loc.GetString("cmd-startworldrule-decs"),
            Loc.GetString("cmd-startworldrule-help"),
            StartWorldRuleCallback,
            StartWorldRuleCallbackHelper);

        _host.RegisterCommand("startworldrulenow",
            Loc.GetString("cmd-startworldrulenow-decs"),
            Loc.GetString("cmd-startworldrulenow-help"),
            StartWorldRuleNowCallback,
            StartWorldRuleCallbackHelper);
    }

    [AdminCommand(AdminFlags.Admin)]
    private void StartWorldRuleCallback(IConsoleShell shell, string argstr, string[] args)
    {
        StartWorldRuleCommand(shell, args);
    }

    [AdminCommand(AdminFlags.Admin)]
    private void StartWorldRuleNowCallback(IConsoleShell shell, string argstr, string[] args)
    {
        StartWorldRuleCommand(shell, args, true);
    }

    private CompletionResult StartWorldRuleCallbackHelper(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "Player");

        if (args.Length == 2)
        {
            var rules = new List<EntProtoId>();
            var entities = EntityQueryEnumerator<RimFortressRuleComponent>();

            while (entities.MoveNext(out var comp))
            {
                rules.AddRange(_table.GetSpawns(comp.WorldEvents));
            }

            return CompletionResult.FromHintOptions(rules.Select(x => x.Id), "Event");
        }

        return CompletionResult.Empty;
    }

    private void StartWorldRuleCommand(IConsoleShell shell, string[] args, bool ignoreDelay = false)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!_player.TryGetSessionByUsername(args[0], out var session)
            || session.AttachedEntity is not { } player)
            return;

        if (!_prototype.TryIndex(args[1], out EntityPrototype? proto))
        {
            shell.WriteLine(Loc.GetString(
                "shell-argument-must-be-prototype",
                ("index", 2),
                ("prototypeName", "cmd-startworldrule-prototype")));
            return;
        }

        var settlements = _world.GetPlayerSettlements(player);

        if (settlements.Count == 0)
        {
            shell.WriteLine(Loc.GetString("cmd-startworldrule-no-settlements"));
            return;
        }

        StartWorldRule(proto, player, _random.Pick(settlements), ignoreDelay);
    }
}
