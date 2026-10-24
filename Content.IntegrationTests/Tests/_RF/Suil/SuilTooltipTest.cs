#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.HotReload;
using Content.Client._RF.Tooltip;
using Content.Client._RF.Tooltip.Controls;
using Content.Client._RF.Tooltip.Prototypes;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The tooltips (<c>elements/tooltip/tooltip.suil</c>): the popup, the base tooltip supplier of the controls and the
/// [tooltip] tag.
/// </summary>
[TestFixture]
public sealed class SuilTooltipTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;
    [SidedDependency(Side.Client)] private readonly ISuilManager _suil = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _ui = null!;

    private static UiElement Field(object view, string id) => (UiElement) ((ISuilView) view).SuilGetField(id)!;

    private IEnumerable<TooltipPopup> Popups()
    {
        var found = new List<TooltipPopup>();
        Collect(_ui.RootControl);
        return found;

        void Collect(Control control)
        {
            if (control is TooltipPopup popup)
                found.Add(popup);

            foreach (var child in control.Children)
            {
                Collect(child);
            }
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void TooltipPopup_Shows_Only_What_The_Definition_Has()
    {
        var popup = new TooltipPopup(new TooltipDefinition { Title = "title", Body = "body" });

        Assert.That(Field(popup, "header").Visible, Is.True);
        Assert.That(Field(popup, "body").Visible, Is.True);
        Assert.That(Field(popup, "icon-panel").Visible, Is.False, "there is no icon");

        popup.SetDefinition(new TooltipDefinition { Title = "", Body = "only the text" });

        Assert.That(Field(popup, "header").Visible, Is.False, "no title and no icon: no header");
        Assert.That(Field(popup, "header-divider").Visible, Is.False);

        popup.SetDefinition(new TooltipDefinition { Title = "title" });
        Assert.That(Field(popup, "body").Visible, Is.False);

        var result = _reload.Reload(popup);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void TooltipPopup_Timer_Is_Only_For_Pinnable_Tooltips()
    {
        var def = new TooltipDefinition { Title = "title" };
        var pinnable = new TooltipPopup(def);
        var plain = new TooltipPopup(def, tracking: false, pinnable: false);

        pinnable.StartTracking();
        plain.StartTracking();

        Assert.That(Field(pinnable, "progress").Visible, Is.True);
        Assert.That(Field(plain, "progress").Visible, Is.False);
        Assert.That(pinnable.Pinned, Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Every_UiElement_Has_The_Base_Tooltip_Supplier()
    {
        var plain = new UiElement();
        var withText = new UiElement { ToolTip = "text" };
        var withDef = new UiElement { TooltipDefinition = new TooltipDefinition { Title = "title" } };
        var controller = _ui.GetUIController<TooltipUIController>();

        Assert.That(plain.TooltipSupplier, Is.Not.Null);
        Assert.That(plain.TrackingTooltip, Is.True, "the tooltip follows the cursor by default");
        Assert.That(plain.TooltipSupplier!(plain), Is.Null, "no data: no tooltip");

        try
        {
            _ui.WindowRoot.AddChild(withText);
            _ui.WindowRoot.AddChild(withDef);

            Assert.That(withText.TooltipSupplier!(withText), Is.Null, "the popup is opened by the supplier itself");
            Assert.That(Popups().Count(), Is.EqualTo(1));

            controller.CloseAll();
            Assert.That(Popups(), Is.Empty);

            withDef.TooltipSupplier!(withDef);
            Assert.That(Popups().Count(), Is.EqualTo(1));
        }
        finally
        {
            controller.CloseAll();
            withText.Orphan();
            withDef.Orphan();
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Tooltip_Tag_Makes_A_Term_With_A_Tooltip()
    {
        var text = new UiElement { Text = "see [tooltip entProto=\"MobHuman\"]human[/tooltip] here" };
        _ui.WindowRoot.AddChild(text);

        try
        {
            _suil.UpdateStyleRecursively(text);
            text.Measure(new Vector2(400, 200));
            text.Arrange(new UIBox2(0, 0, 400, 200));

            var term = text.Children.OfType<TooltipTermLabel>().SingleOrDefault();
            Assert.That(term, Is.Not.Null);
            Assert.That(term!.TooltipDefinition, Is.Not.Null);
            Assert.That(term.TooltipPinnable, Is.True);
            Assert.That(term.TooltipDelay, Is.EqualTo(0f));
            Assert.That(term.HasSuilStyleClass("tooltip-term"), Is.True);
        }
        finally
        {
            text.Orphan();
        }
    }
}
