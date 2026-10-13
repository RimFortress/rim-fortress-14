using Content.Client._RF.CSS;
using Content.Client.Credits;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

using Content.Client._RF.UserInterface;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Info.Controls;

/// <summary>
/// A row with the buttons of the bug report and of the credits, the <see cref="CssControl"/> version of
/// <see cref="Content.Client.Info.DevInfoBanner"/>. The space between the buttons is <c>gap</c>.
/// </summary>
[UsedImplicitly]
public sealed partial class RfDevInfoBanner : CssBoxContainer
{
    [Dependency] private IConfigurationManager _cfg = null!;
    [Dependency] private IUriOpener _uriOpener = null!;

    public RfDevInfoBanner()
    {
        IoCManager.InjectDependencies(this);
        FlexDirection = CssFlexDirection.Row;

        var bugReport = _cfg.GetCVar(CCVars.InfoLinksBugReport);
        if (bugReport != "")
        {
            var reportButton = new CssButton { Text = Loc.GetString("server-info-report-button") };
            reportButton.AddCssStyleClass(CssClass.RowButton);
            reportButton.OnPressed += _ => _uriOpener.OpenUri(bugReport);
            AddChild(reportButton);
        }

        var creditsButton = new CssButton { Text = Loc.GetString("server-info-credits-button") };
        creditsButton.AddCssStyleClass(CssClass.RowButton);
        creditsButton.OnPressed += _ => new CreditsWindow().Open();
        AddChild(creditsButton);
    }
}
