using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Robust.Shared.Console;

namespace Content.Server._RF.GameTicking.Commands;

[AnyCommand]
public sealed partial class RimFortressJoinGameCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "rfjoingame";
    public string Description => "";
    public string Help => "";

    public RimFortressJoinGameCommand()
    {
        IoCManager.InjectDependencies(this);
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var ticker = _entManager.System<GameTicker>();

        if (shell.Player == null
            || ticker.PlayerGameStatuses.TryGetValue(shell.Player.UserId, out var status)
            && status == PlayerGameStatus.JoinedGame
            || ticker.RunLevel == GameRunLevel.PreRoundLobby)
        {
            shell.WriteError("You cannot join game at this time.");
            return;
        }

        ticker.BeforeSpawn(shell.Player, null, true, null);
    }
}

