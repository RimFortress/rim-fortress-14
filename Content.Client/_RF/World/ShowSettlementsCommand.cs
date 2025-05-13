using Robust.Shared.Console;

namespace Content.Client._RF.World;


public sealed class ShowSettlementsCommand : IConsoleCommand
{
    public string Command => "showsettlements";
    public string Description => "Shows coordinates of player settlements";
    public string Help => $"{Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var world = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<RimFortressWorldSystem>();
        world.EnableOverlay ^= true;
    }
}
