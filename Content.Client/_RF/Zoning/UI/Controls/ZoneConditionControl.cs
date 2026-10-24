using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.Zoning;

namespace Content.Client._RF.Zoning.UI.Controls;

public sealed partial class ZoneConditionControl : RfBoxContainer
{
    public ZoneConditionControl()
    {
        InitializeComponent();
    }

    public ZoneConditionControl(IZoneConditionDesc desc) : this()
    {
        var state = desc.IsMet ? "met" : "not-met";
        IsMetOutline.AddSuilStyleClass(state);
        IsMet.AddSuilStyleClass(state);

        TextLabel.Text = $"[b]{desc.Text}[/b]";
        Progress.Text = $"[b]{desc.Progress}[/b]";

        if (desc.SubDescriptions == null)
            return;

        foreach (var subDesc in desc.SubDescriptions)
        {
            SubDescriptions.AddChild(new ZoneConditionControl(subDesc));
        }
    }
}
