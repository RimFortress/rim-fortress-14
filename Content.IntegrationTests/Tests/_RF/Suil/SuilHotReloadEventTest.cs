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
        => _reload.Reload(view, new MemorySource(new Dictionary<string, string> { ["/test/view.suil"] = SuilTestText.AsView(suil) }), SuilGlobals.None);

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

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Call_Passes_Its_Arguments_From_The_File()
    {
        var view = new TestView();

        var result = Reload(view, """
            box {
                button #submit (min-width: 4, on-toggled => { @Mark("hi", args.pressed, calc(#submit.min-width + 1)) })
            }
            """);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));

        Toggle(view.Submit!, true);

        Assert.That(view.Calls, Is.EqualTo(new[] { "mark:hi:True:5" }));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Call_Without_Parentheses_In_A_Block_Takes_Nothing()
    {
        var view = new TestView();

        var plain = Reload(view, "box { button #submit (on-pressed => { @OnSubmit }) }");
        Assert.That(plain.Success, Is.False);
        Assert.That(string.Join("\n", plain.Errors), Does.Contain("@OnSubmit"));

        var withArguments = Reload(view, "box { button #submit (on-pressed => { @OnSubmit(args) }) }");
        Assert.That(withArguments.Success, Is.True, string.Join("\n", withArguments.Errors));

        Press(view.Submit!);
        Assert.That(view.Calls, Is.EqualTo(new[] { "submit:RfButtonEventArgs" }));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Call_Of_A_Controller_Is_Made_If_The_Build_Knew_It()
    {
        var suil = Robust.Shared.IoC.IoCManager.Resolve<ISuilManager>();
        var seen = new List<object?>();
        suil.Calls.Add("Controllers.Test.Pick", new[] { typeof(int), typeof(string) }, a => seen.AddRange(a));
        suil.Calls.Add("Controllers.Test.Ping", Type.EmptyTypes, _ => seen.Add("ping"));

        var view = new TestView();

        var known = Reload(view, "box { button #submit (on-pressed => { @Controllers.Test.Pick(3, \"x\") }) }");
        Assert.That(known.Success, Is.True, string.Join("\n", known.Errors));

        Press(view.Submit!);
        Assert.That(seen, Is.EqualTo(new object?[] { 3, "x" }));

        var withoutParentheses = Reload(view, "box { button #submit (on-pressed => @Controllers.Test.Ping) }");
        Assert.That(withoutParentheses.Success, Is.True, string.Join("\n", withoutParentheses.Errors));

        Press(view.Submit!);
        Assert.That(seen.Last(), Is.EqualTo("ping"));

        var unknown = Reload(view, "box { button #submit (on-pressed => { @Systems.Test.Other(1) }) }");
        Assert.That(unknown.Success, Is.False);
        Assert.That(string.Join("\n", unknown.Errors), Does.Contain("build it again"));
    }

    // ---- Blocks ----

    [Test]
    [RunOnSide(Side.Client)]
    public void Ampersand_Is_The_View_Itself()
    {
        var view = new TestView();

        var result = Reload(view, """
            box (min-width: 5) {
                button #submit (on-pressed => { &(min-width +: 2, add .touched, visible: !&.visible); #submit(min-width: 9) })
            }
            """);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));

        Press(view.Submit!);

        Assert.Multiple(() =>
        {
            Assert.That(view.MinWidth, Is.EqualTo(7f));
            Assert.That(view.HasSuilStyleClass("touched"), Is.True);
            Assert.That(view.Visible, Is.False);
            Assert.That(view.Submit!.MinWidth, Is.EqualTo(9f));
        });
    }

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
                    #submit(min-height: 7, add .pressed);
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
            Assert.That(view.Submit!.MinHeight, Is.EqualTo(7f), "#submit is the control of the event");
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

    // ---- Bool expressions ----

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Handler_Assigns_A_Property_From_The_Properties_Of_Other_Elements()
    {
        var view = new TestView();
        var result = Reload(view, """
            box {
                button #submit (on-toggled => {
                    #target (disabled: !#source.disabled, toggle-mode: #source.toggle-mode && args.pressed || #target.toggle-mode)
                })
                button #source
                button #target
            }
            """);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));

        var source = (RfButton) view.GetChild(1);
        var target = (RfButton) view.GetChild(2);

        Toggle(view.Submit!, true);
        Assert.That(target.Disabled, Is.True, "!false");
        Assert.That(target.ToggleMode, Is.False);

        source.Disabled = true;
        source.ToggleMode = true;
        Toggle(view.Submit!, true);
        Assert.That(target.Disabled, Is.False, "!true");
        Assert.That(target.ToggleMode, Is.True);

        Toggle(view.Submit!, false);
        Assert.That(target.ToggleMode, Is.True, "false || the old value of the target");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Variables_Of_The_File_Are_Folded_Before_The_Handler_Runs()
    {
        var view = new TestView();
        Reload(view, """
            let always = true;
            let never = false;
            box {
                button #submit (on-pressed => { #submit(disabled: #peer.disabled && <always>, toggle-mode: #peer.disabled && <never> || <always>) })
                button #peer
            }
            """);

        ((RfButton) view.GetChild(1)).Disabled = true;
        Press(view.Submit!);

        Assert.That(view.Submit!.Disabled, Is.True);
        Assert.That(view.Submit.ToggleMode, Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Set_Takes_Its_State_From_An_Expression()
    {
        var view = new TestView();
        Reload(view, """
            box {
                button #submit (toggle-mode: true, on-toggled => { #panel (set .open !#menu.disabled && args.pressed, set :busy #menu.toggle-mode || false) })
                button #menu
                box #panel
            }
            """);

        var menu = (RfButton) view.GetChild(1);
        var panel = (RfBoxContainer) view.GetChild(2);

        Toggle(view.Submit!, true);
        Assert.That(panel.HasSuilStyleClass("open"), Is.True);
        Assert.That(panel.HasSuilPseudoStyleClass("busy"), Is.False);

        menu.Disabled = true;
        menu.ToggleMode = true;
        Toggle(view.Submit!, true);
        Assert.That(panel.HasSuilStyleClass("open"), Is.False, "the menu is disabled");
        Assert.That(panel.HasSuilPseudoStyleClass("busy"), Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Reference_May_Go_Through_The_Fields_Of_A_View_And_Name_The_Sender()
    {
        var view = new TestView();
        Reload(view, """
            box {
                button #submit (on-pressed => { #submit(disabled: !#panel/header.disabled && !#submit.toggle-mode) })
                test-inner #panel
            }
            """);

        var header = ((TestInnerView) view.GetChild(1)).Header;

        Press(view.Submit!);
        Assert.That(view.Submit!.Disabled, Is.True);

        header.Disabled = true;
        Press(view.Submit);
        Assert.That(view.Submit.Disabled, Is.False);
    }

    [TestCase("box { button #submit (on-pressed => { #submit(disabled: #peer.no-such) }) button #peer }", "no readable bool property")]
    [TestCase("box { button #submit (on-pressed => { #submit(disabled: #peer.min-width) }) button #peer }", "no readable bool property")]
    [TestCase("box { button #submit (on-pressed => { #submit(disabled: #nowhere.disabled) }) }", "no element with the id")]
    [TestCase("box { button #submit (on-pressed => { #submit(min-width: !#submit.disabled) }) }", "bool property")]
    [TestCase("box { button #submit (on-pressed => { #submit(no-such: !#submit.disabled) }) }", "no property")]
    [TestCase("box { button #submit (on-pressed => { #submit(set .a #peer.nothing) }) button #peer }", "no readable bool property")]
    [TestCase("box { button #submit (on-pressed => { #submit(disabled: args.nope) }) }", "args.nope")]
    [RunOnSide(Side.Client)]
    public void A_Mistake_In_An_Expression_Leaves_Everything_As_It_Was(string suil, string expected)
    {
        var view = new TestView();
        Reload(view, "box { button #submit (on-pressed => @Refresh) }");
        var submit = view.Submit!;

        var result = Reload(view, suil);

        Assert.That(result.Success, Is.False, suil);
        Assert.That(string.Join("\n", result.Errors), Does.Contain(expected), suil);
        Assert.That(view.Submit, Is.SameAs(submit));
        Press(view.Submit!);
        Assert.That(view.Calls, Is.EqualTo(new[] { "refresh" }));
    }

    // ---- Number expressions ----

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Handler_Assigns_A_Number_From_The_Properties_Of_Other_Elements()
    {
        var view = new TestView();
        var result = Reload(view, """
            box {
                button #submit (on-pressed => {
                    #target (
                        min-width: calc(#source.min-width * 2 + 1px),
                        min-height: #source.min-height,
                        max-width: calc(min(#source.min-width, 4) + abs(-3) + clamp(1, #source.min-height, 6))
                    )
                })
                button #source
                button #target
            }
            """);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));

        var source = (RfButton) view.GetChild(1);
        var target = (RfButton) view.GetChild(2);
        source.MinWidth = 5;
        source.MinHeight = 9;

        Press(view.Submit!);

        Assert.That(target.MinWidth, Is.EqualTo(11f));
        Assert.That(target.MinHeight, Is.EqualTo(9f), "a lone reference to a number is read as a number");
        Assert.That(target.MaxWidth, Is.EqualTo(13f), "4 + 3 + 6");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Short_Operations_Change_A_Property_In_Place()
    {
        var view = new TestView();
        Reload(view, """
            let step = 2;
            box {
                button #submit (min-width: 10, min-height: 8, max-width: 6, max-height: 20, on-pressed => {
                    #submit(min-width +: 5, min-height *: <step>, max-width -: calc(#other.min-width * 3), max-height /: #other.min-width)
                })
                button #other (min-width: 4)
            }
            """);

        Press(view.Submit!);

        Assert.That(view.Submit!.MinWidth, Is.EqualTo(15f));
        Assert.That(view.Submit.MinHeight, Is.EqualTo(16f));
        Assert.That(view.Submit.MaxWidth, Is.EqualTo(-6f));
        Assert.That(view.Submit.MaxHeight, Is.EqualTo(5f));

        Press(view.Submit);
        Assert.That(view.Submit.MinWidth, Is.EqualTo(20f), "every time the event is raised");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Variables_Of_The_File_Are_Folded_Into_The_Number_Expression()
    {
        var view = new TestView();
        Reload(view, """
            let gap = 3;
            box {
                button #submit (min-width: 1, on-pressed => { #submit(min-width: calc(#other.min-width * (<gap> + 1)), min-height +: calc(<gap> * 2)) })
                button #other (min-width: 4)
            }
            """);

        Press(view.Submit!);

        Assert.That(view.Submit!.MinWidth, Is.EqualTo(16f));
        Assert.That(view.Submit.MinHeight, Is.EqualTo(6f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Number_May_Come_From_A_View_Inside()
    {
        var view = new TestView();
        Reload(view, """
            box {
                button #submit (on-pressed => { #submit(min-width: calc(#panel/header.min-width + 1), min-height +: #panel/header.min-width) })
                test-inner #panel
            }
            """);

        ((TestInnerView) view.GetChild(1)).Header.MinWidth = 7;
        Press(view.Submit!);

        Assert.That(view.Submit!.MinWidth, Is.EqualTo(8f));
        Assert.That(view.Submit.MinHeight, Is.EqualTo(7f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Number_That_Is_Not_A_Number_Of_The_Property_Is_Held_To_Its_Type()
    {
        var view = new TestView();
        Reload(view, "box { button #submit (toggle-mode: true, on-pressed => { #submit(min-width: calc(#other.min-width / 3)) }) button #other }");

        ((RfButton) view.GetChild(1)).MinWidth = 10;
        Press(view.Submit!);

        Assert.That(view.Submit!.MinWidth, Is.EqualTo(10f / 3f).Within(1e-5), "min-width is a float: no rounding");
    }

    [TestCase("box { button #submit (on-pressed => { #submit(min-width: calc(#peer.disabled)) }) button #peer }", "no readable numeric property")]
    [TestCase("box { button #submit (on-pressed => { #submit(min-width: calc(#peer.no-such)) }) button #peer }", "no readable numeric property")]
    [TestCase("box { button #submit (on-pressed => { #submit(min-width: calc(#nowhere.min-width)) }) }", "no element with the id")]
    [TestCase("box { button #submit (on-pressed => { #submit(disabled: calc(1 + 2)) }) }", "'disabled'")]
    [TestCase("box { button #submit (on-pressed => { #submit(text: calc(1 + 2)) }) }", "'text'")]
    [TestCase("box { button #submit (on-pressed => { #submit(disabled +: 1) }) }", "works on a numeric property")]
    [TestCase("box { button #submit (on-pressed => { #submit(text *: 2) }) }", "works on a numeric property")]
    [TestCase("box { button #submit (on-pressed => { #submit(no-such +: 1) }) }", "no property")]
    [TestCase("box { button #submit (on-pressed => { #submit(min-width +: #nowhere.min-width) }) }", "no element with the id")]
    [RunOnSide(Side.Client)]
    public void A_Mistake_In_A_Number_Expression_Leaves_Everything_As_It_Was(string suil, string expected)
    {
        var view = new TestView();
        Reload(view, "box { button #submit (on-pressed => @Refresh) }");
        var submit = view.Submit!;

        var result = Reload(view, suil);

        Assert.That(result.Success, Is.False, suil);
        Assert.That(string.Join("\n", result.Errors), Does.Contain(expected), suil);
        Assert.That(view.Submit, Is.SameAs(submit));
        Press(view.Submit!);
        Assert.That(view.Calls, Is.EqualTo(new[] { "refresh" }));
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
    [TestCase("box { button #submit (on-pressed => { #submit(no-such-property: 1) }) }", "no property")]
    [TestCase("box { button #submit (on-pressed => { #submit(min-width: red) }) }", "min-width")]
    [TestCase("box { button #submit (on-pressed => { #submit(set .a args.pressed) }) }", "args.pressed")]
    [TestCase("box { button #submit (on-toggled => { #submit(set .a args.nope) }) }", "args.nope")]
    [TestCase("box { box #x (on-pressed => @Refresh) }", "no event")]
    [TestCase("box { box #x (on-pressed => { #x(add :loading) }) }", "no event")]
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

    string ISuilView.SuilViewName => "Inner";

    object? ISuilView.SuilRootChildren => null;

    object? ISuilView.SuilContent => null;

    object? ISuilView.SuilContentBuilt => null;

    void ISuilView.SuilSetContent(object? collection) { }

    IReadOnlyList<string> ISuilView.SuilFieldIds { get; } = new[] { "header" };

    Type? ISuilView.SuilFieldType(string id) => id == "header" ? typeof(RfButton) : null;

    object? ISuilView.SuilGetField(string id) => id == "header" ? Header : null;

    bool ISuilView.SuilSetField(string id, object? value) => false;

    bool ISuilView.SuilTryGetMethod(string name, int arity, out Type[]? parameters)
    {
        parameters = null;
        return false;
    }

    void ISuilView.SuilInvoke(string name, object?[] arguments)
    {
    }
}
