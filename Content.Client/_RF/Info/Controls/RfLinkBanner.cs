using Content.Client._RF.Suil;
using Content.Client.Info;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Client.UserInterface.Systems.Guidebook;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;

using Content.Client._RF.UserInterface;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Info.Controls;

/// <summary>
/// A row of buttons that open the rules, the guidebook, the changelog and the links to the community of the server,
/// the <see cref="UiElement"/> version of <see cref="Content.Client.Info.LinkBanner"/>.
/// </summary>
/// <remarks>
/// It is a <see cref="RfBoxContainer"/> (<c>rf-link-banner</c>, a row by default), so the space between the
/// buttons is <c>gap</c>, the buttons are plain <c>rf-button</c>s. A link whose CVar is empty is hidden.
/// </remarks>
[UsedImplicitly]
public sealed partial class RfLinkBanner : RfBoxContainer
{
    [Dependency] private IConfigurationManager _cfg = null!;
    [Dependency] private IUriOpener _uriOpener = null!;

    private readonly List<(CVarDef<string> CVar, RfButton Button)> _infoLinks = new();

    public RfLinkBanner()
    {
        IoCManager.InjectDependencies(this);
        FlexDirection = SuilFlexDirection.Row;

        var rulesButton = AddButton("server-info-rules-button");
        rulesButton.OnPressed += _ => new RulesAndInfoWindow().Open();

        AddInfoButton("server-info-discord-button", CCVars.InfoLinksDiscord);
        AddInfoButton("server-info-website-button", CCVars.InfoLinksWebsite);
        AddInfoButton("server-info-wiki-button", CCVars.InfoLinksWiki);
        AddInfoButton("server-info-forum-button", CCVars.InfoLinksForum);
        AddInfoButton("server-info-telegram-button", CCVars.InfoLinksTelegram);

        var guidebookButton = AddButton("server-info-guidebook-button");
        guidebookButton.OnPressed += _ => UserInterfaceManager.GetUIController<GuidebookUIController>().ToggleGuidebook();

        var changelogButton = new RfChangelogButton();
        changelogButton.AddSuilStyleClass(SuilClass.RowButton);
        changelogButton.OnPressed += _ => UserInterfaceManager.GetUIController<ChangelogUIController>().ToggleWindow();
        AddChild(changelogButton);
    }

    private RfButton AddButton(string loc)
    {
        var button = new RfButton { Text = Loc.GetString(loc) };
        button.AddSuilStyleClass(SuilClass.RowButton);
        AddChild(button);
        return button;
    }

    private void AddInfoButton(string loc, CVarDef<string> cVar)
    {
        var button = AddButton(loc);
        button.OnPressed += _ => _uriOpener.OpenUri(_cfg.GetCVar(cVar));
        _infoLinks.Add((cVar, button));
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        // The banner is constructed before the client connects to the server, so the links are checked when it shows.
        foreach (var (cVar, button) in _infoLinks)
        {
            button.Visible = _cfg.GetCVar(cVar) != "";
        }
    }
}
