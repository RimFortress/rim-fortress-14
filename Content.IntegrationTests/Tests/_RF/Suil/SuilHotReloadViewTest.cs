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
    public GeneratedHotReloadView()
    {
        InitializeComponent();
    }
}
