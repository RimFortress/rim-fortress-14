using Content.Client._RF.UserInterface.Controls.NpcJobs;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controllers;

public sealed class NpcJobsUiController : UIController
{
    public NpcJobsPriorityWindow? PriorityWindow;
    public NpcJobsSettingsWindow? SettingsWindow;

    public void TogglePriorityWindow()
    {
        EnsurePriorityWindow();

        if (PriorityWindow!.IsOpen)
            PriorityWindow.Close();
        else
        {
            PriorityWindow.Open();
            PriorityWindow.BuildTable();
        }
    }

    private void EnsurePriorityWindow()
    {
        if (PriorityWindow is { Disposed: false })
            return;

        PriorityWindow = UIManager.CreateWindow<NpcJobsPriorityWindow>();
        PriorityWindow.EnsureSetup();

        LayoutContainer.SetAnchorPreset(PriorityWindow, LayoutContainer.LayoutPreset.Center);
    }

    public void ToggleSettingsWindow()
    {
        EnsureSettingsWindow();

        if (SettingsWindow!.IsOpen)
            SettingsWindow.Close();
        else
        {
            if (PriorityWindow is { Disposed: false })
                SettingsWindow.Open(PriorityWindow.GlobalPosition + PriorityWindow.SizeBox.Center - SettingsWindow.MinSize / 2);
            else
                SettingsWindow.Open();

            SettingsWindow.Build();
        }
    }

    private void EnsureSettingsWindow()
    {
        if (SettingsWindow is { Disposed: false })
            return;

        SettingsWindow = UIManager.CreateWindow<NpcJobsSettingsWindow>();
        SettingsWindow.EnsureSetup();

        LayoutContainer.SetAnchorPreset(SettingsWindow, LayoutContainer.LayoutPreset.Center);
    }
}
