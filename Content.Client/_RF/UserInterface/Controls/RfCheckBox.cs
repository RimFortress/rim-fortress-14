using Content.Client._RF.Suil;
using JetBrains.Annotations;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A toggleable button with a check box, the <see cref="UiElement"/> version of
/// <see cref="Robust.Client.UserInterface.Controls.CheckBox"/>.
/// </summary>
/// <remarks>
/// The box is <see cref="Indicator"/> (style class <see cref="SuilClass.Indicator"/>, plus
/// <see cref="SuilClass.Checked"/> while checked) and the text is <see cref="Label"/> (style class
/// <see cref="SuilClass.Label"/>). Select them with <c>rf-check-box .indicator.checked</c>; the check box itself
/// matches <c>:checked</c>, <c>:hover</c>, <c>:active</c> and <c>:disabled</c>. The space between them is
/// <see cref="Gap"/> (SUIL <c>gap</c>).
/// </remarks>
[Virtual, UsedImplicitly]
public partial class RfCheckBox : RfButton
{
    private readonly RfBoxContainer _box;

    /// <summary>
    /// The check box.
    /// </summary>
    [PublicAPI, ViewVariables]
    public UiElement Indicator { get; }

    /// <summary>
    /// The text next to the check box.
    /// </summary>
    [PublicAPI, ViewVariables]
    public UiElement Label { get; }

    /// <summary>
    /// Should the check box be to the left or the right of the label.
    /// </summary>
    [PublicAPI, ViewVariables]
    public bool LeftAlign
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            Label.FlexGrow = value ? 0f : 1f;

            if (value)
            {
                Indicator.SetPositionFirst();
                Label.SetPositionInParent(1);
            }
            else
            {
                Label.SetPositionFirst();
                Indicator.SetPositionInParent(1);
            }
        }
    } = true;

    /// <summary>
    /// The text displayed next to the check box.
    /// </summary>
    [PublicAPI, ViewVariables]
    public new string? Text
    {
        get => Label.Text;
        set => Label.Text = value;
    }

    public RfCheckBox()
    {
        ToggleMode = true;

        Indicator = new UiElement
        {
            AlignSelf = SuilAlignSelf.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };
        Indicator.AddSuilStyleClass(SuilClass.Indicator);

        Label = new UiElement
        {
            AlignSelf = SuilAlignSelf.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };
        Label.AddSuilStyleClass(SuilClass.Label);

        _box = new RfBoxContainer
        {
            FlexDirection = SuilFlexDirection.Row,
            MouseFilter = MouseFilterMode.Ignore,
        };
        _box.AddChild(Indicator);
        _box.AddChild(Label);
        AddChild(_box);
    }

    protected override void OnGapChanged()
    {
        _box.Gap = Gap;
    }

    protected override void StateChanged()
    {
        base.StateChanged();

        // Called by the base constructor chain before the indicator exists.
        if (Indicator == null!)
            return;

        if (Pressed)
            Indicator.AddSuilStyleClass(SuilClass.Checked);
        else
            Indicator.RemoveSuilStyleClass(SuilClass.Checked);
    }
}
