using System.Linq;
using JetBrains.Annotations;
using Robust.Shared.Console;

namespace Content.Client._RF.Suil.HotReload;

/// <summary>
/// <c>suil_reload</c>: without arguments waits for a click on a control and reloads the view it belongs to; with a path
/// reloads the views built from that file; with <c>all</c> reloads every view.
/// </summary>
[UsedImplicitly]
public sealed partial class SuilReloadCommand : LocalizedCommands
{
    [Dependency] private ISuilHotReload _reload = null!;

    public override string Command => "suil_reload";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        switch (args)
        {
            case []:
                _reload.StartPicking();
                shell.WriteLine(Loc.GetString("cmd-suil_reload-picking"));
                return;
            case ["all"]:
                Report(shell, _reload.ReloadAll());
                return;
            case [var path]:
                var results = _reload.ReloadPath(path);

                if (results.Count == 0)
                    shell.WriteError(Loc.GetString("cmd-suil_reload-none", ("path", path)));
                else
                    Report(shell, results);

                return;
            default:
                shell.WriteError(Loc.GetString("cmd-suil_reload-help", ("command", Command)));
                return;
        }
    }

    private void Report(IConsoleShell shell, IReadOnlyList<SuilReloadResult> results)
    {
        foreach (var result in results)
        {
            if (result.Success)
            {
                shell.WriteLine(Loc.GetString("cmd-suil_reload-done", ("path", result.View.SuilPath)));
            }
            else
            {
                shell.WriteError(Loc.GetString("cmd-suil_reload-failed", ("path", result.View.SuilPath)));

                foreach (var error in result.Errors)
                {
                    shell.WriteError("  " + error);
                }
            }

            foreach (var warning in result.Warnings)
            {
                shell.WriteLine("  ! " + warning);
            }
        }

        shell.WriteLine(Loc.GetString("cmd-suil_reload-total", ("count", results.Count)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        var options = new List<CompletionOption> { new("all", Loc.GetString("cmd-suil_reload-all-hint")) };

        foreach (var entry in Content.Suil.SuilViewRegistry.Snapshot())
        {
            if (entry.TryGetView(out var view))
                options.Add(new CompletionOption(view.SuilPath));
        }

        return CompletionResult.FromHintOptions(options.DistinctBy(o => o.Value), Loc.GetString("cmd-suil_reload-path-hint"));
    }
}
