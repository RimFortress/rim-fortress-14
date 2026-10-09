using Content.Client._RF.CSS;
using JetBrains.Annotations;
using Robust.Client.Console;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A button that executes a console command, the <see cref="CssButton"/> version of
/// <see cref="Content.Client.Administration.UI.CustomControls.CommandButton"/>.
/// It hides itself when the player can't run the command.
/// </summary>
[Virtual, UsedImplicitly]
public partial class CssCommandButton : CssButton
{
    [Dependency] private IClientConGroupController _conGroup = null!;
    [Dependency] private IClientConsoleHost _consoleHost = null!;

    public string? Command { get; set; }

    public CssCommandButton()
    {
        IoCManager.InjectDependencies(this);

        OnPressed += Execute;
    }

    protected virtual bool CanPress()
    {
        return string.IsNullOrEmpty(Command) || _conGroup.CanCommand(Command.Split(' ')[0]);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        if (!CanPress())
            Visible = false;
    }

    protected virtual void Execute(CssButtonEventArgs args)
    {
        if (!string.IsNullOrEmpty(Command))
            _consoleHost.ExecuteCommand(Command);
    }
}

/// <summary>
/// A <see cref="CssCommandButton"/> that opens a window instead of running the command.
/// The command is still used to decide whether the button is shown.
/// </summary>
[UsedImplicitly]
public sealed partial class CssUICommandButton : CssCommandButton
{
    [Dependency] private IDynamicTypeFactory _factory = null!;

    public Type? WindowType { get; set; }

    protected override void Execute(CssButtonEventArgs args)
    {
        if (WindowType == null)
            return;

        ((BaseWindow)_factory.CreateInstance(WindowType)).OpenCentered();
    }
}
