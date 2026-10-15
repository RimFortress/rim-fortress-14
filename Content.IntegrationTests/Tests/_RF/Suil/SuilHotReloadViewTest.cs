#nullable enable
using System.Linq;
using Content.Client._RF.Suil.HotReload;
using Content.Client._RF.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// A view the generator wrote: the hot reload must make the tree that <c>InitializeComponent()</c> made.
/// </summary>
[TestFixture]
public sealed class SuilHotReloadViewTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Reload_Builds_The_Tree_Of_The_Generated_Code()
    {
        var view = new GeneratedHotReloadView();
        var built = Describe(view);
        var submit = view.HotSubmit;

        Assert.That(((ISuilView) view).SuilPath, Is.EqualTo("/_RF/tests/hot-reload-view"));

        var result = _reload.Reload(view);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(result.Warnings, Is.Empty);
        Assert.That(Describe(view), Is.EqualTo(built), "the same tree, built from the file by the interpreter");
        Assert.That(view.HotSubmit, Is.SameAs(submit), "the control with a field is kept");
        Assert.That(result.Dependencies, Does.Contain("/_RF/tests/hot-reload-view.suil"));
        Assert.That(result.Dependencies, Does.Contain("/layout.suil"), "the globals are what the view depends on as well");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Handlers_Of_The_File_Replace_The_Compiled_Ones()
    {
        var view = new GeneratedHotReloadView();
        var submit = view.HotSubmit;

        Press(submit);
        Assert.That(view.Submitted, Is.EqualTo(1), "the handler of the compiled code");
        Assert.That(submit.HasSuilStyleClass("pressed"), Is.True);
        Assert.That(view.HotNested.MinHeight, Is.EqualTo(3f));
        Assert.That(view.HotNested.MinWidth, Is.EqualTo(4f), "min-width +: 4");
        Assert.That(view.HotNested.Visible, Is.False, "!disabled: the button is disabled in the file");

        submit.RemoveSuilStyleClass("pressed");

        var result = _reload.Reload(view);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(view.HotSubmit, Is.SameAs(submit));

        // the file disables the button again; the expression reads the state of the button when the button is pressed
        submit.Disabled = false;
        view.HotNested.Visible = false;
        view.HotNested.MinWidth = 0;
        view.HotNested.MinHeight = 0;

        Press(submit);

        Assert.That(view.Submitted, Is.EqualTo(2), "once: the compiled handler is off, the one of the reload is on");
        Assert.That(submit.HasSuilStyleClass("pressed"), Is.True);
        Assert.That(view.HotNested.Visible, Is.True, "the expression of the reload read the new state: !false");
        Assert.That(view.HotNested.MinWidth, Is.EqualTo(4f), "once: the short operation of the compiled handler is off as well");
        Assert.That(view.HotNested.MinHeight, Is.EqualTo(3f));

        _reload.Reload(view);
        Press(submit);
        Assert.That(view.Submitted, Is.EqualTo(3), "and still once after another reload");
    }

    private static void Press(RfButton button)
    {
        var field = typeof(RfButton).GetField("OnPressed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var args = new RfButtonEventArgs(button, new GUIBoundKeyEventArgs(
            Robust.Shared.Input.EngineKeyFunctions.UIClick, Robust.Shared.Input.BoundKeyState.Down, default, true,
            System.Numerics.Vector2.Zero, System.Numerics.Vector2.Zero));
        ((Delegate?) field.GetValue(button))?.DynamicInvoke(args);
    }

    /// <summary>
    /// The shape of the tree with what the file sets, which a comparison of two builds needs.
    /// </summary>
    private static string Describe(Control control)
    {
        var text = control.GetType().Name;

        if (control is Content.Client._RF.Suil.UiElement element)
        {
            text += $"#{element.SuilStyleIdentifier}.{string.Join(".", element.SuilStyleClasses.OrderBy(c => c))}";
            text += $"[{element.MinWidth}]";
        }

        if (control is RfButton button)
            text += $"<{button.Text}|{button.Disabled}>";

        return text + "(" + string.Join(",", control.Children.Select(Describe)) + ")";
    }
}

[SuilView("/_RF/tests/hot-reload-view")]
public sealed partial class GeneratedHotReloadView : RfBoxContainer
{
    public int Submitted;

    public GeneratedHotReloadView()
    {
        InitializeComponent();
    }

    private void OnHotSubmit() => Submitted++;
}
