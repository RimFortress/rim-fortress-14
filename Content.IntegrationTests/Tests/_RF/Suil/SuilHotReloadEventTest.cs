#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.HotReload;
using Content.Client._RF.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The handlers of the events in the hot reload: <c>on-pressed =&gt; @Method</c> and the blocks of statements are subscribed
/// from the file, not from the compiled code.
/// </summary>
[TestFixture]
public sealed class SuilHotReloadEventTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;

    private sealed class MemorySource(Dictionary<string, string> files) : ISuilFileSource
    {
        public SuilFile Load(string path)
            => files.TryGetValue(path, out var text)
                ? SuilParser.ParseFile(text, path)
                : throw new FileNotFoundException(path);

        public IReadOnlyList<string> List(string directory) => files.Keys.ToList();
    }

    private SuilReloadResult Reload(ISuilView view, string suil)
        => _reload.Reload(view, new MemorySource(new Dictionary<string, string> { ["/test/view.suil"] = suil }), SuilGlobals.None);

    private static GUIBoundKeyEventArgs Click()
        => new(EngineKeyFunctions.UIClick, BoundKeyState.Down, default, true, Vector2.Zero, Vector2.Zero);

    /// <summary>
    /// Raises an event of a button the way the button does: the delegate of the event is invoked.
    /// </summary>
    private static void Raise(RfButton button, string evt, object args)
    {
        var field = typeof(RfButton).GetField(evt, BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((Delegate?) field.GetValue(button))?.DynamicInvoke(args);
    }

    private static void Press(RfButton button) => Raise(button, "OnPressed", new RfButtonEventArgs(button, Click()));

    private static void Toggle(RfButton button, bool pressed)
        => Raise(button, "OnToggled", new RfButtonToggledEventArgs(pressed, button, Click()));

    // ---- Calls ----

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Handler_Of_The_File_Calls_The_Method_Of_The_View()
    {
        var view = new TestView();

        var result = Reload(view, "box { button #submit (on-pressed => @OnSubmit, on-toggled => @OnToggle) }");

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));

        Press(view.Submit!);
        Toggle(view.Submit!, true);

        Assert.That(view.Calls, Is.EqualTo(new[] { "submit:RfButtonEventArgs", "toggle:True" }));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Method_Without_Arguments_Is_Fine_For_Any_Event()
    {
        var view = new TestView();
        Reload(view, "box { button #submit (on-pressed => @Refresh) }");

        Press(view.Submit!);

        Assert.That(view.Calls, Is.EqualTo(new[] { "refresh" }));
    }

    // ---- Blocks ----

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Block_Runs_Its_Statements_In_Order()
    {
        var view = new TestView();

        var result = Reload(view, """
            box {
                button #submit (on-pressed => {
                    @Refresh;
                    #target (min-width: 33, add .open :loading, remove .gone);
                    &(min-height: 7, add .pressed);
                    @Refresh
                })
                box #target .gone
            }
            """);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));

        var target = (RfBoxContainer) view.GetChild(1);
        Assert.That(target.HasSuilStyleClass("gone"), Is.True);

        Press(view.Submit!);

        Assert.Multiple(() =>
        {
            Assert.That(view.Calls, Is.EqualTo(new[] { "refresh", "refresh" }));
            Assert.That(target.MinWidth, Is.EqualTo(33f));
            Assert.That(view.Submit!.MinHeight, Is.EqualTo(7f), "& is the control of the event");
            Assert.That(target.HasSuilStyleClass("open"), Is.True);
            Assert.That(target.HasSuilPseudoStyleClass("loading"), Is.True);
            Assert.That(target.HasSuilStyleClass("gone"), Is.False);
            Assert.That(view.Submit.HasSuilStyleClass("pressed"), Is.True);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Set_Takes_The_State_From_The_Arguments_Of_The_Event()
    {
        var view = new TestView();
        Reload(view, """
            box {
                button #submit (toggle-mode: true, on-toggled => { #panel (set .open :expanded args.pressed) })
                box #panel
            }
            """);

        var panel = (RfBoxContainer) view.GetChild(1);

        Toggle(view.Submit!, true);
        Assert.That(panel.HasSuilStyleClass("open"), Is.True);
        Assert.That(panel.HasSuilPseudoStyleClass("expanded"), Is.True);

        Toggle(view.Submit!, false);
        Assert.That(panel.HasSuilStyleClass("open"), Is.False);
        Assert.That(panel.HasSuilPseudoStyleClass("expanded"), Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Handler_Can_Change_Any_Element_Of_The_View_Not_Only_The_Children_Of_Its_Own_Control()
    {
        var view = new TestView();

        var result = Reload(view, """
            box {
                box #menu {
                    box { button #submit (on-pressed => { #footer (add .shown); #deep-item (min-width: 6); #menu (add .opened) }) }
                }
                box { box { box #deep-item } }
                box #footer
            }
            """);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));

        Press(view.Submit!);

        var menu = (RfBoxContainer) view.GetChild(0);
        var deep = (RfBoxContainer) view.GetChild(1).GetChild(0).GetChild(0);
        var footer = (RfBoxContainer) view.GetChild(2);

        Assert.Multiple(() =>
        {
            Assert.That(footer.HasSuilStyleClass("shown"), Is.True, "another branch");
            Assert.That(menu.HasSuilStyleClass("opened"), Is.True, "an ancestor");
            Assert.That(deep.MinWidth, Is.EqualTo(6f), "deep in a branch of its own");
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Targets_Include_Elements_Without_A_Field_And_The_Root()
    {
        var view = new TestView();
        Reload(view, """
            box #whole {
                button #submit (on-pressed => { #whole (add .clicked); #brand-new (min-width: 5) })
                box #brand-new
            }
            """);

        Press(view.Submit!);

        Assert.That(view.HasSuilStyleClass("clicked"), Is.True);
        Assert.That(((RfBoxContainer) view.GetChild(1)).MinWidth, Is.EqualTo(5f));
    }

    // ---- Reloading ----

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Reload_Does_Not_Double_The_Handlers_Of_A_Control_That_Is_Kept()
    {
        var view = new TestView();
        const string file = "box { button #submit (on-pressed => @Refresh) }";

        Reload(view, file);
        var submit = view.Submit;
        Reload(view, file);
        Reload(view, file);

        Assert.That(view.Submit, Is.SameAs(submit), "kept");

        Press(view.Submit!);

        Assert.That(view.Calls, Is.EqualTo(new[] { "refresh" }), "once, not once for every reload");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Handler_That_Left_The_File_Is_Taken_Off()
    {
        var view = new TestView();
        Reload(view, "box { button #submit (on-pressed => @Refresh) }");
        Reload(view, "box { button #submit }");

        Press(view.Submit!);

        Assert.That(view.Calls, Is.Empty);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Handlers_Of_The_Compiled_Code_Are_Taken_Off_On_The_First_Reload()
    {
        var view = new TestView(compiledHandler: true);

        Reload(view, "box { button #submit }");
        Assert.That(view.CompiledUnsubscribed, Is.EqualTo(1));

        Reload(view, "box { button #submit }");
        Assert.That(view.CompiledUnsubscribed, Is.EqualTo(1), "only once");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Handlers_Of_A_Control_That_Is_Replaced_Are_Taken_Off()
    {
        var view = new TestView();
        Reload(view, "box { button #submit (on-pressed => @Refresh) }");
        var old = view.Submit!;

        // the button is not in the file any more, so it is out of the tree, and its handler goes with it
        Reload(view, "box { box }");
        Press(old);

        Assert.That(view.Calls, Is.Empty, "the old control no longer calls the view");
    }

    // ---- Paths ----

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Path_Goes_Through_The_Fields_Of_A_View_Inside()
    {
        var view = new TestView();
        var result = Reload(view, """
            box {
                button #submit (on-pressed => { #panel/header (min-width: 9, add .seen) })
                test-inner #panel
            }
            """);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));

        var panel = (TestInnerView) view.GetChild(1);
        Press(view.Submit!);

        Assert.That(panel.Header.MinWidth, Is.EqualTo(9f));
        Assert.That(panel.Header.HasSuilStyleClass("seen"), Is.True);
    }

    // ---- Mistakes ----

    [TestCase("box { button #submit (on-nope => @Refresh) }", "no event")]
    [TestCase("box { button #submit (on-pressed => @Missing) }", "no such method")]
    [TestCase("box { button #submit (on-pressed => @OnToggle) }", "takes")]
    [TestCase("box { button #submit (on-pressed => { #nowhere (min-width: 1) }) }", "no element with the id")]
    [TestCase("box { button #submit (on-pressed => { #nowhere (add .a) }) }", "no element with the id")]
    [TestCase("box { button #submit (on-pressed => { &(no-such-property: 1) }) }", "no property")]
    [TestCase("box { button #submit (on-pressed => { &(min-width: red) }) }", "min-width")]
    [TestCase("box { button #submit (on-pressed => { &(set .a args.pressed) }) }", "args.pressed")]
    [TestCase("box { button #submit (on-toggled => { &(set .a args.nope) }) }", "args.nope")]
    [TestCase("box { box #x (on-pressed => @Refresh) }", "no event")]
    [TestCase("box { box #x (on-pressed => { &(add :loading) }) }", "no event")]
    [RunOnSide(Side.Client)]
    public void A_Mistake_In_A_Handler_Leaves_Everything_As_It_Was(string suil, string expected)
    {
        var view = new TestView();
        Reload(view, "box { button #submit (on-pressed => @Refresh) }");
        var submit = view.Submit!;

        var result = Reload(view, suil);

        Assert.That(result.Success, Is.False, suil);
        Assert.That(string.Join("\n", result.Errors), Does.Contain(expected), suil);

        // the old tree and its handler are still there
        Assert.That(view.Submit, Is.SameAs(submit));
        Press(view.Submit!);
        Assert.That(view.Calls, Is.EqualTo(new[] { "refresh" }));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Pseudo_Class_Needs_A_Control_Of_Suil()
    {
        var view = new TestView();

        var result = Reload(view, "box { button #submit (on-pressed => { #v (add :loading) }) sprite-view #v }");

        Assert.That(result.Success, Is.False);
        Assert.That(string.Join("\n", result.Errors), Does.Contain("no pseudo-classes"));
    }
}

/// <summary>
/// A view inside a view with a public field, the way a generated one has: <c>header</c>.
/// </summary>
[SuilTag("test-inner")]
public sealed class TestInnerView : RfBoxContainer, ISuilView
{
    public RfButton Header = new();

    public TestInnerView()
    {
        AddChild(Header);
    }

    string ISuilView.SuilPath => "/test/inner";

    IReadOnlyList<string> ISuilView.SuilFieldIds { get; } = new[] { "header" };

    Type? ISuilView.SuilFieldType(string id) => id == "header" ? typeof(RfButton) : null;

    object? ISuilView.SuilGetField(string id) => id == "header" ? Header : null;

    bool ISuilView.SuilSetField(string id, object? value) => false;

    bool ISuilView.SuilTryGetMethod(string name, out Type? argumentType)
    {
        argumentType = null;
        return false;
    }

    void ISuilView.SuilInvoke(string name, object? argument)
    {
    }
}
