#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.HotReload;
using Content.Client._RF.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The hot reload of the views: the tree is built again from the files and put on the live controls.
/// </summary>
/// <remarks>
/// The views here implement <see cref="ISuilView"/> by hand, the way the generator does, and the files are in memory.
/// </remarks>
[TestFixture]
public sealed class SuilHotReloadTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;
    [SidedDependency(Side.Client)] private readonly ISuilManager _suil = null!;

    private sealed class MemorySource(Dictionary<string, string> files) : ISuilFileSource
    {
        public SuilFile Load(string path)
            => files.TryGetValue(path, out var text)
                ? SuilParser.ParseFile(text, path)
                : throw new FileNotFoundException(path);

        public IReadOnlyList<string> List(string directory) => files.Keys.ToList();
    }

    private static MemorySource Files(string view, params (string Path, string Text)[] more)
    {
        var files = new Dictionary<string, string> { ["/test/view.suil"] = SuilTestText.AsView(view) };

        foreach (var (path, text) in more)
        {
            files[path] = text;
        }

        return new MemorySource(files);
    }

    private SuilReloadResult Reload(TestView view, string suil, params (string Path, string Text)[] more)
        => _reload.Reload(view, Files(suil, more), SuilGlobals.None);

    // ---- Building ----

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Tree_Is_Built_And_The_Fields_Are_Bound()
    {
        var view = new TestView();

        var result = Reload(view, """
            box .root #main (min-width: 40) {
                +button #submit .primary (text: "Go", disabled)
                box { button .plain }
            }
            """);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(view.SuilStyleIdentifier, Is.EqualTo("main"));
            Assert.That(view.HasSuilStyleClass("root"), Is.True);
            Assert.That(view.MinWidth, Is.EqualTo(40f), "a plain number is a float for an element property");
            Assert.That(view.ChildCount, Is.EqualTo(2));
            Assert.That(view.Submit, Is.Not.Null);
            Assert.That(view.Submit!.Parent, Is.SameAs(view));
            Assert.That(view.Submit.Text, Is.EqualTo("Go"));
            Assert.That(view.Submit.Disabled, Is.True);
            Assert.That(view.Submit.SuilStyleIdentifier, Is.EqualTo("submit"));
            Assert.That(view.Submit.HasSuilStyleClass("primary"), Is.True);
            Assert.That(view.GetChild(1).ChildCount, Is.EqualTo(1));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Control_With_A_Field_Is_Kept_And_The_Others_Are_New()
    {
        var view = new TestView();

        Assert.That(Reload(view, """
            box {
                button #submit (text: "One")
                button .loose
            }
            """).Success, Is.True);

        var submit = view.Submit;
        var loose = view.GetChild(1);
        var clicked = 0;
        submit!.OnPressed += _ => clicked++;

        var result = Reload(view, """
            box {
                button .loose
                button #submit (text: "Two")
            }
            """);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(view.Submit, Is.SameAs(submit), "the field keeps the control, so the handlers still work");
            Assert.That(view.Submit!.Text, Is.EqualTo("Two"));
            Assert.That(view.GetChild(1), Is.SameAs(submit), "it moved to the place the file gave it");
            Assert.That(view.GetChild(0), Is.Not.SameAs(loose), "a control without an id is new");
            Assert.That(view.ChildCount, Is.EqualTo(2));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void What_The_File_No_Longer_Sets_Goes_Back()
    {
        var view = new TestView();
        Reload(view, "box { button #submit .a .b (text: \"Hi\", disabled) }");

        var submit = view.Submit!;
        Assert.That(submit.Disabled, Is.True);

        var result = Reload(view, "box { button #submit .b (text: \"Hi\") }");

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.Multiple(() =>
        {
            Assert.That(view.Submit, Is.SameAs(submit));
            Assert.That(submit.Disabled, Is.False, "disabled is not in the file now: the value from before the file is back");
            Assert.That(submit.HasSuilStyleClass("a"), Is.False);
            Assert.That(submit.HasSuilStyleClass("b"), Is.True);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Controls_Of_The_Code_Are_Not_Touched_Outside_The_Fields()
    {
        var view = new TestView();
        Reload(view, "box { button #submit }");

        // the code sets a class the file doesn't know: it is not the file's to take away
        view.Submit!.AddSuilStyleClass("from-code");
        Reload(view, "box { button #submit .x }");

        Assert.That(view.Submit.HasSuilStyleClass("from-code"), Is.True);
        Assert.That(view.Submit.HasSuilStyleClass("x"), Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Imports_And_Templates_Are_Expanded_Again()
    {
        var view = new TestView();

        var result = Reload(view, """
            use "/test/parts";
            box { my-button (text: "Hello") }
            """,
            ("/test/parts.suil", "let my-button = (text) { button #submit (text: <text>) }"));

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(view.Submit!.Text, Is.EqualTo("Hello"));
        Assert.That(result.Dependencies, Is.EquivalentTo(new[] { "/test/view.suil", "/test/parts.suil" }));
    }

    // ---- Mistakes ----

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Mistake_Leaves_The_Tree_As_It_Was()
    {
        var view = new TestView();
        Reload(view, "box { button #submit (text: \"Keep\") }");
        var submit = view.Submit!;

        foreach (var broken in new[]
                 {
                     "box { button #submit (no-such-property: 1) }",
                     "box { no-such-control #submit }",
                     "box { button #submit (text: ) }",
                     "box { button #submit (min-width: red) }",
                     "box { button #submit button",
                 })
        {
            var result = Reload(view, broken);

            Assert.That(result.Success, Is.False, broken);
            Assert.That(result.Errors, Is.Not.Empty, broken);
            Assert.That(view.Submit, Is.SameAs(submit), broken);
            Assert.That(view.Submit!.Text, Is.EqualTo("Keep"), broken);
            Assert.That(view.ChildCount, Is.EqualTo(1), broken);
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Missing_File_Is_An_Error()
    {
        var view = new TestView();

        var result = _reload.Reload(view, new MemorySource(new Dictionary<string, string>()), SuilGlobals.None);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Root_Can_Not_Change_Its_Type()
    {
        var view = new TestView();
        Reload(view, "box { button #submit }");

        var result = Reload(view, "button { }");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Single(), Does.Contain("build the project"));
        Assert.That(view.ChildCount, Is.EqualTo(1));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void An_Id_Without_A_Field_Is_Built_With_A_Warning()
    {
        var view = new TestView();

        var result = Reload(view, "box { button #brand-new }");

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(result.Warnings.Single(), Does.Contain("brand-new").And.Contain("no field"));
        Assert.That(view.ChildCount, Is.EqualTo(1));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Field_Does_Not_Accept_Another_Type()
    {
        var view = new TestView();

        // `submit` is a field of RfButton, a box is not one
        var result = Reload(view, "box { box #submit }");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Single(), Does.Contain("field"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void An_Id_That_Left_The_File_Is_Reported()
    {
        var view = new TestView();
        Reload(view, "box { button #submit }");

        var result = Reload(view, "box { }");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Warnings.Single(), Does.Contain("submit"));
    }

    // ---- Hard and soft properties ----

    [Test]
    [RunOnSide(Side.Client)]
    public void What_The_File_Assigns_Is_Hard_Unless_It_Has_A_Question_Mark()
    {
        var view = new TestView();

        var result = Reload(view, "box { button #submit (text: \"Go\", disabled: ?true) }");

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(view.Submit!.SuilIsHard("text"), Is.True);
        Assert.That(view.Submit.SuilIsHard("disabled"), Is.False, "soft");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Styles_Do_Not_Override_A_Hard_Property_Even_With_Important()
    {
        _suil.ClearTestStylesheet();
        _suil.LoadSuilString("""
            button.styled { min-width: 70px; min-height: 80px; }
            button.styled { min-height: 90px !important; }
            """);

        var view = new TestView();
        Reload(view, "box { button #submit .styled (min-width: 10, min-height: ?20) }");

        _suil.UpdateTestStyle(view.Submit!);

        Assert.Multiple(() =>
        {
            Assert.That(view.Submit!.MinWidth, Is.EqualTo(10f), "hard: the rule can't change it");
            Assert.That(view.Submit.MinHeight, Is.EqualTo(90f), "soft: the rule, and the important one first, wins");
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Property_Changes_Between_Hard_And_Soft_When_The_File_Does()
    {
        _suil.ClearTestStylesheet();
        _suil.LoadSuilString("button.styled { min-width: 70px; }");

        var view = new TestView();
        Reload(view, "box { button #submit .styled (min-width: 10) }");
        var submit = view.Submit!;
        _suil.UpdateTestStyle(submit);
        Assert.That(submit.MinWidth, Is.EqualTo(10f));

        // now the styles have it
        Reload(view, "box { button #submit .styled (min-width: ?10) }");
        _suil.UpdateTestStyle(submit);
        Assert.That(submit.SuilIsHard("min-width"), Is.False);
        Assert.That(submit.MinWidth, Is.EqualTo(70f));

        // and the file has it again
        Reload(view, "box { button #submit .styled (min-width: 12) }");
        _suil.UpdateTestStyle(submit);
        Assert.That(submit.MinWidth, Is.EqualTo(12f));

        // a property that left the file is no one's but the styles'
        Reload(view, "box { button #submit .styled }");
        Assert.That(submit.SuilIsHard("min-width"), Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void An_Exclamation_Mark_After_A_Value_Is_An_Error()
    {
        var view = new TestView();

        var result = Reload(view, "box { button #submit (min-width: 10!) }");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Errors.Single(), Does.Contain("hard by default"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Root_Properties_Are_Hard_As_Well()
    {
        var view = new TestView();

        Reload(view, "box (min-width: 33) { }");

        Assert.That(view.SuilIsHard("min-width"), Is.True);
    }

    // ---- The rest ----

    [Test]
    [RunOnSide(Side.Client)]
    public void The_View_Is_Told_It_Was_Reloaded()
    {
        var view = new TestView();

        Reload(view, "box { }");
        Reload(view, "box { }");
        Reload(view, "box { no-such-control }");

        Assert.That(view.Reloaded, Is.EqualTo(2), "a reload that failed is not a reload");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Reloading_Twice_Gives_The_Same_Tree()
    {
        var view = new TestView();
        const string file = "box .a { button #submit .b (text: \"x\") box { button .c } }";

        Reload(view, file);
        var first = Shape(view);
        Reload(view, file);

        Assert.That(Shape(view), Is.EqualTo(first));
        Assert.That(view.ChildCount, Is.EqualTo(2));
    }

    private static string Shape(Control control)
        => control.GetType().Name + "(" + string.Join(",", control.Children.Select(Shape)) + ")";
}

/// <summary>
/// A view the way the generator writes it, by hand: the root is a box, <c>submit</c> is a field.
/// </summary>
public sealed class TestView : RfBoxContainer, ISuilView, ISuilReloadHandler
{
    public RfButton? Submit;

    public int Reloaded;

    /// <summary>What the handlers of the file called.</summary>
    public readonly List<string> Calls = new();

    /// <summary>The unsubscribers a generated <c>InitializeComponent()</c> would have registered.</summary>
    public int CompiledUnsubscribed;

    public TestView(bool compiledHandler = false)
    {
        // what the generated InitializeComponent() does at its end
        SuilViewRegistry.Register(this, compiledHandler ? new Action[] { () => CompiledUnsubscribed++ } : null);
    }

    public void Refresh() => Calls.Add("refresh");

    public void OnSubmit(RfButtonEventArgs args) => Calls.Add("submit:" + args.GetType().Name);

    public void OnToggle(RfButtonToggledEventArgs args) => Calls.Add("toggle:" + args.Pressed);

    public void Mark(string text, bool pressed, double width) => Calls.Add($"mark:{text}:{pressed}:{width}");

    string ISuilView.SuilPath => "/test/view";

    string ISuilView.SuilViewName => "Test";

    object? ISuilView.SuilRootChildren => null;

    object? ISuilView.SuilContent => null;

    object? ISuilView.SuilContentBuilt => null;

    void ISuilView.SuilSetContent(object? collection) { }

    IReadOnlyList<string> ISuilView.SuilFieldIds { get; } = new[] { "submit" };

    Type? ISuilView.SuilFieldType(string id) => id == "submit" ? typeof(RfButton) : null;

    object? ISuilView.SuilGetField(string id) => id == "submit" ? Submit : null;

    bool ISuilView.SuilSetField(string id, object? value)
    {
        if (id != "submit" || value is not RfButton button)
            return false;

        Submit = button;
        return true;
    }

    bool ISuilView.SuilTryGetMethod(string name, int arity, out Type[]? parameters)
    {
        switch (name)
        {
            case nameof(Refresh) when arity == 0:
                parameters = Array.Empty<Type>();
                return true;
            case nameof(OnSubmit) when arity == 1:
                parameters = new[] { typeof(RfButtonEventArgs) };
                return true;
            case nameof(OnToggle) when arity == 1:
                parameters = new[] { typeof(RfButtonToggledEventArgs) };
                return true;
            case nameof(Mark) when arity == 3:
                parameters = new[] { typeof(string), typeof(bool), typeof(double) };
                return true;
            default:
                parameters = null;
                return false;
        }
    }

    void ISuilView.SuilInvoke(string name, object?[] arguments)
    {
        switch (name)
        {
            case nameof(Refresh):
                Refresh();
                break;
            case nameof(OnSubmit):
                OnSubmit((RfButtonEventArgs) arguments[0]!);
                break;
            case nameof(OnToggle):
                OnToggle((RfButtonToggledEventArgs) arguments[0]!);
                break;
            case nameof(Mark):
                Mark((string) arguments[0]!, (bool) arguments[1]!, (double) arguments[2]!);
                break;
        }
    }

    void ISuilReloadHandler.OnSuilReloaded() => Reloaded++;
}
