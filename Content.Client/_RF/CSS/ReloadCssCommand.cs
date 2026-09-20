using JetBrains.Annotations;
using Robust.Shared.Console;

namespace Content.Client._RF.CSS;

[UsedImplicitly]
public sealed class ReloadCssCommand : LocalizedCommands
{
    public override string Command => "rldcss";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        IoCManager.Resolve<ICssManager>().Reload();
    }
}
