#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.HotReload;
using Content.Client._RF.UserInterface.Controls;
using Content.Client._RF.Workshops.UI;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.Workshops.Prototypes;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The workshop (<c>elements/workshops/workshops.suil</c>): the menu and the rows of its lists.
/// </summary>
[TestFixture]
public sealed class SuilWorkshopsTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;
    [SidedDependency(Side.Client)] private readonly IPrototypeManager _proto = null!;
    [SidedDependency(Side.Client)] private readonly ISuilManager _suil = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _ui = null!;

    private static UiElement Field(object view, string id) => (UiElement) ((ISuilView) view).SuilGetField(id)!;

    [Test]
    [RunOnSide(Side.Client)]
    public void WorkshopMenu_Is_Built_Laid_Out_And_Reloads()
    {
        var menu = new WorkshopMenu();

        menu.Measure(new System.Numerics.Vector2(850, 600));
        menu.Arrange(new UIBox2(0, 0, 850, 600));

        var search = Field(menu, "search");
        var recipes = Field(menu, "recipes-scroll");
        var contents = Field(menu, "contents-box");
        var results = Field(menu, "results-box");

        Assert.That(menu.MinSize, Is.EqualTo(new System.Numerics.Vector2(850, 600)));
        Assert.That(search.Width, Is.GreaterThan(200f), "the search takes what the buttons leave");
        Assert.That(recipes.Height, Is.GreaterThan(300f), "the list of the recipes takes the height");
        Assert.That(contents.Parent!.Width, Is.EqualTo(results.Parent!.Width).Within(2f), "the contents and the results are of one width");

        var supplied = (RfOptionButton) ((ISuilView) menu).SuilGetField("supplied-stock")!;
        Assert.That(supplied, Is.Not.Null);

        var result = _reload.Reload(menu);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
    }

    private static void Collect(Control control, string styleClass, List<UiElement> into)
    {
        if (control is UiElement element && element.HasSuilStyleClass(styleClass))
            into.Add(element);

        foreach (var child in control.Children)
        {
            Collect(child, styleClass, into);
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void WorkshopMenu_Dividers_Of_The_Header_And_Of_The_Lists_Are_On_One_Line()
    {
        var menu = new WorkshopMenu();
        _ui.WindowRoot.AddChild(menu);

        try
        {
            _suil.UpdateStyleRecursively(menu);
            menu.Measure(new System.Numerics.Vector2(850, 600));
            menu.Arrange(new UIBox2(0, 0, 850, 600));

            var dividers = new List<UiElement>();
            Collect(menu, "low-divider", dividers);

            // the one under the title of the window, the one between the columns, the one of the header, the one of the
            // lists, the one of the bottom strip
            Assert.That(dividers, Has.Count.EqualTo(5));
            var header = dividers[2];
            var lists = dividers[3];

            Assert.That(header.GlobalPosition.X + header.Width / 2f,
                Is.EqualTo(lists.GlobalPosition.X + lists.Width / 2f).Within(1f));
        }
        finally
        {
            menu.Orphan();
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void WorkshopMenu_Texts_Of_The_Bottom_Strip_Are_Centered_In_Its_Height()
    {
        var menu = new WorkshopMenu();
        menu.Measure(new System.Numerics.Vector2(850, 600));
        menu.Arrange(new UIBox2(0, 0, 850, 600));

        foreach (var id in new[] { "user-label", "group-label" })
        {
            var control = Field(menu, id);
            var parent = control.Parent!;
            Assert.That(control.Height, Is.LessThan(parent.Height), $"{id}: not stretched, a stretched text sticks to the top");
            Assert.That(control.Position.Y + control.Height / 2f, Is.EqualTo(parent.Height / 2f).Within(1f), id);
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Rows_Of_The_Lists_Are_Built_And_Reload()
    {
        Control[] views =
        [
            new RecipeResultButton(),
            new WorkshopRecipeIngredient(),
            new WorkshopRecipeButton(),
            new RecipeQueueButton(),
        ];

        foreach (var view in views)
        {
            var result = _reload.Reload((ISuilView) view);
            Assert.That(result.Success, Is.True, $"{view.GetType().Name}: {string.Join("\n", result.Errors)}");
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void RecipeButton_Reserves_The_Space_Of_The_Marker_And_Divider_Is_A_Strip()
    {
        var button = new WorkshopRecipeButton();
        var marker = Field(button, "group-marker");

        Assert.That(marker.ReservesSpace, Is.True, "a recipe hides it, but the names stay in one column");

        button.Measure(new System.Numerics.Vector2(400, 100));
        button.Arrange(new UIBox2(0, 0, 400, 100));

        var main = Field(button, "button");
        Assert.That(main.Width, Is.EqualTo(396f).Within(1f), "the button takes the width of the list less its margin");
        Assert.That(marker.Height, Is.GreaterThan(main.Height * 0.5f), "a strip along the button");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Steps_Of_Other_Recipes_Are_Shifted_In_The_Queue()
    {
        var recipe = _proto.EnumeratePrototypes<WorkshopRecipePrototype>().FirstOrDefault();

        if (recipe == null)
            Assert.Ignore("no recipes in the prototypes");

        var step = new RecipeQueueButton(recipe!.ID, subRecipe: true);
        var normal = new RecipeQueueButton(recipe.ID);

        Assert.That(step.Margin.Left - normal.Margin.Left, Is.EqualTo(20f), "the whole item is shifted, not its content");
        Assert.That(Field(step, "close-button").Visible, Is.False, "a step has no cross");
        Assert.That(Field(normal, "close-button").Visible, Is.True);
        Assert.That(normal.MouseFilter, Is.EqualTo(Control.MouseFilterMode.Stop), "the clicks go to the item");
    }
}
