using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared.Localizations;
using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RF.Workshops.UI;

public sealed partial class RecipeQueueButton : RfBoxContainer
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;

    public event Action? OnRemoved;
    public event Action? OnToggleRepeat;
    public event Action? OnToggleSuspend;

    /// <summary>
    /// The shift to the right of a step of a recipe that is made for another one, in pixels.
    /// </summary>
    private const float SubRecipeIndent = 20f;

    // the left margin of the item in the file
    private const float BaseMargin = 2f;

    /// <summary>
    /// How often the time left is refreshed, in seconds.
    /// </summary>
    private const float UpdateRate = 0.25f;

    private readonly TimeSpan _endAt;

    public RecipeQueueButton()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
    }

    public RecipeQueueButton(
        ProtoId<WorkshopRecipePrototype> protoId,
        TimeSpan? endAt = null,
        bool subRecipe = false,
        bool repeat = false,
        bool suspended = false,
        bool active = false)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        if (!_proto.Resolve(protoId, out var proto))
            return;

        _endAt = endAt ?? TimeSpan.Zero;

        Icon.SetPrototype(proto.Result);
        RecipeName.Text = _proto.Index(proto.Result).Name;
        CloseButton.Visible = !subRecipe;

        Status.Text = GetStatus(active, repeat, suspended);

        if (subRecipe)
            MarginLeft = LengthUnit.Px(BaseMargin + SubRecipeIndent);
        else
        {
            ToolTip = Loc.GetString("workshop-menu-recipe-queue-tooltip");
            CloseButton.OnPressed += _ => OnRemoved?.Invoke();
        }

        // the time left, while the recipe is made
        Scope.Every(UpdateRate, UpdateRemainingTime, runOnEnter: true);
    }

    private void UpdateRemainingTime()
    {
        if (_endAt == TimeSpan.Zero)
            return;

        RemainingTime.Text = ContentLocalizationManager.FormatTimeShort(_endAt - _timing.CurTime);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.UseSecondary)
            OnToggleRepeat?.Invoke();

        if (args.Function == EngineKeyFunctions.Use)
            OnToggleSuspend?.Invoke();
    }

    private static string GetStatus(bool active = false, bool repeat = false, bool suspended = false)
    {
        var text = string.Empty;

        if (active)
            text = Loc.GetString("workshop-menu-recipe-queue-status-active");

        if (repeat)
            text = $"{text}{Loc.GetString("workshop-menu-recipe-queue-status-repeat")}";

        if (suspended)
            text = $"{text}{Loc.GetString("workshop-menu-recipe-queue-status-suspended")}";

        return text;
    }
}

