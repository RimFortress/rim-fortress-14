using Content.Client._RF.World.Settlements;
using Robust.Shared.Console;

namespace Content.Client._RF.World;

public sealed class ShowSettlementsCommand : IConsoleCommand
{
    public string Command => "showsettlements";
    public string Description => "Shows the settlements: their color, the area they reach, what they own and what it is worth";
    public string Help => $"{Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var system = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SettlementDebugSystem>();
        system.Enabled ^= true;
    }
}
