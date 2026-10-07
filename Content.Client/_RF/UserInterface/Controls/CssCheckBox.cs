using Content.Client._RF.CSS;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A toggleable button with a check box, the <see cref="CssControl"/> version of
/// <see cref="Robust.Client.UserInterface.Controls.CheckBox"/>.
/// </summary>
/// <remarks>
/// The box is <see cref="Indicator"/> (style class <see cref="IndicatorStyleClass"/>, plus
/// <see cref="CheckedStyleClass"/> while checked) and the text is <see cref="Label"/> (style class
/// <see cref="LabelStyleClass"/>). Select them with <c>css-check-box > .indicator.checked</c>; the check box itself
/// matches <c>:checked</c>, <c>:hover</c>, <c>:active</c> and <c>:disabled</c>.
/// </remarks>
[Virtual, UsedImplicitly]
public class CssCheckBox : CssButton
{
    public const string IndicatorStyleClass = "indicator";
    public const string LabelStyleClass = "label";
    public const string CheckedStyleClass = "checked";

    /// <summary>
    /// The check box.
    /// </summary>
    [PublicAPI, ViewVariables]
    public CssControl Indicator { get; }

    /// <summary>
    /// The text next to the check box.
    /// </summary>
    [PublicAPI, ViewVariables]
    public CssControl Label { get; }

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
            Label.HorizontalExpand = !value;

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

    public CssCheckBox()
    {
        ToggleMode = true;

        Indicator = new CssControl
        {
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };
        Indicator.AddCssStyleClass(IndicatorStyleClass);

        Label = new CssControl
        {
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };
        Label.AddCssStyleClass(LabelStyleClass);

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            MouseFilter = MouseFilterMode.Ignore,
        };
        box.AddChild(Indicator);
        box.AddChild(Label);
        AddChild(box);
    }

    protected override void StateChanged()
    {
        base.StateChanged();

        // Called by the base constructor chain before the indicator exists.
        if (Indicator == null!)
            return;

        if (Pressed)
            Indicator.AddCssStyleClass(CheckedStyleClass);
        else
            Indicator.RemoveCssStyleClass(CheckedStyleClass);
    }
}
