using Content.Client._RF.World.Settlements;
using Robust.Shared.Console;

namespace Content.Client._RF.World;

public sealed class ShowSettlementsCommand : LocalizedEntityCommands
{
    public override string Command => "showsettlements";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var system = EntityManager.System<SettlementDebugSystem>();
        system.Enabled ^= true;
    }
}
