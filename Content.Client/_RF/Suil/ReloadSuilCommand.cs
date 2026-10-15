using Content.Client._RF.CSS;
using JetBrains.Annotations;
using Robust.Shared.Console;

namespace Content.Suil;

[UsedImplicitly]
public sealed class ReloadSuilCommand : LocalizedCommands
{
    public override string Command => "rldsuil";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        IoCManager.Resolve<ISuilManager>().Reload();
    }
}
