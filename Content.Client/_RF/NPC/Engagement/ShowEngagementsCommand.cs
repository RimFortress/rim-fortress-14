using Robust.Shared.Console;

namespace Content.Client._RF.NPC.Engagement;

public sealed class ShowEngagementsCommand : LocalizedEntityCommands
{
    public override string Command => "showengagements";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var system = EntityManager.System<EngagementDebugSystem>();

        if (args.Length == 0)
        {
            system.Enabled ^= true;
            return;
        }

        if (args[0] == "off")
        {
            system.Enabled = false;
            return;
        }

        system.Filter = argStr.Trim();
        system.Enabled = true;
    }
}
