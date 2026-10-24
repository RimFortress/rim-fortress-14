using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.NPC.UI;

/// <summary>
/// A field of an object: its name with the type, and under them the value. All the texts are markup.
/// </summary>
public sealed partial class AiFieldRow : RfBoxContainer
{
    public AiFieldRow(string key, string type, string value)
    {
        InitializeComponent();

        Key.Text = $"[bold]{key}[/bold]";
        Type.Text = $":{type}";
        Value.Text = value;
    }
}

/// <summary>
/// A line with the name on the left and the value on the right. All the texts are markup.
/// </summary>
public sealed partial class AiLabelRow : RfBoxContainer
{
    public AiLabelRow(string left, string right)
    {
        InitializeComponent();

        Left.Text = $"[bold]{left}[/bold]";
        Right.Text = $"[bold]{right}[/bold]";
    }
}

/// <summary>
/// The dark line between the entries of a list.
/// </summary>
public sealed partial class AiSeparator : UiElement
{
    public AiSeparator()
    {
        InitializeComponent();
    }
}
