using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Info.Controls;

/// <summary>
/// A tab of <see cref="PopInfoWindow"/>: shows the information about a pop and refreshes it while the tab is visible.
/// </summary>
public abstract class InfoTab : RfBoxContainer, IInfoTab
{
    /// <summary>
    /// The pop the tab shows, <c>null</c> until the window gives one.
    /// </summary>
    protected EntityUid? Uid { get; private set; }

    /// <param name="refreshSeconds">
    /// How often the information is refreshed while the tab is visible, 0 for a tab that is refreshed by its events.
    /// </param>
    protected InfoTab(float refreshSeconds = 1f)
    {
        if (refreshSeconds > 0f)
            Scope.Every(refreshSeconds, Refresh);
    }

    /// <summary>
    /// Fills the tab with the information about the pop. Called when the tab is chosen and after that at an interval.
    /// </summary>
    protected abstract void Update(EntityUid uid);

    public void UpdateInfo(EntityUid uid)
    {
        Uid = uid;
        Update(uid);
    }

    /// <summary>
    /// Fills the tab again with the pop it shows.
    /// </summary>
    protected void Refresh()
    {
        if (Uid is { } uid)
            Update(uid);
    }
}
