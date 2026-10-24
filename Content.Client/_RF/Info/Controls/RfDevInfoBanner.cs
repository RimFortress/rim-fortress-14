using Content.Suil;
using Content.Client._RF.Suil;
using Content.Client.Credits;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

using Content.Client._RF.UserInterface;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Info.Controls;

/// <summary>
/// A row with the buttons of the bug report and of the credits, the <see cref="UiElement"/> version of
/// <see cref="Content.Client.Info.DevInfoBanner"/>. The space between the buttons is <c>gap</c>.
/// </summary>
[UsedImplicitly]
[SuilTag("dev-info-banner")]
public sealed partial class RfDevInfoBanner : RfBoxContainer
{
    [Dependency] private IConfigurationManager _cfg = null!;
    [Dependency] private IUriOpener _uriOpener = null!;

    public RfDevInfoBanner()
    {
        IoCManager.InjectDependencies(this);
        FlexDirection = SuilFlexDirection.Row;

        var bugReport = _cfg.GetCVar(CCVars.InfoLinksBugReport);
        if (bugReport != "")
        {
            var reportButton = new RfButton { Text = Loc.GetString("server-info-report-button") };
            reportButton.AddSuilStyleClass(SuilClass.RowButton);
            reportButton.OnPressed += _ => _uriOpener.OpenUri(bugReport);
            AddChild(reportButton);
        }

        var creditsButton = new RfButton { Text = Loc.GetString("server-info-credits-button") };
        creditsButton.AddSuilStyleClass(SuilClass.RowButton);
        creditsButton.OnPressed += _ => new CreditsWindow().Open();
        AddChild(creditsButton);
    }
}
