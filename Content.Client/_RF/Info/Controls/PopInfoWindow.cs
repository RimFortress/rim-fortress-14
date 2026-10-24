using Content.Client._RF.Skills;
using Content.Client._RF.UserInterface.Controls;
using Content.Shared.Follower;
using Content.Shared.Humanoid;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Info.Controls;

public sealed partial class PopInfoWindow : RfWindow
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    /// <summary>
    /// The names of the tabs in the order of the file: the title of a tab is <c>pop-info-{name}-tab-title</c>.
    /// </summary>
    private static readonly string[] TabNames = ["skills", "jobs", "health", "mood", "opinion", "inventory"];

    private EntityUid? _uid;

    public PopInfoWindow()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        for (var i = 0; i < TabNames.Length; i++)
        {
            Tab.Tabs[i].Title = Loc.GetString($"pop-info-{TabNames[i]}-tab-title");
        }

        Tab.OnTabChanged += args => UpdateTab(args.Tab?.Page);

        FollowButton.OnPressed += _ =>
        {
            if (_entity.GetNetEntity(_uid) is { } uid)
                _net.SendSystemNetworkMessage(new FollowEntityRequest(uid));
        };
    }

    public void SetInfo(EntityUid uid)
    {
        if (!_entity.TryGetComponent(uid, out MetaDataComponent? meta)
            || !_entity.TryGetComponent(uid, out HumanoidProfileComponent? appearance))
            return;

        _uid = uid;
        var sex = appearance.Sex;

        NameLabel.Text = Loc.GetString("pop-info-summary",
            ("name", meta.EntityName),
            ("species", Loc.GetString(_prototype.Index(appearance.Species).Name)),
            ("sex", sex.ToString().ToLowerInvariant()),
            ("age", appearance.Age),
            ("profession", _entity.System<SkillsSystem>().SkillProfession(uid)));

        Sprite.SetEntity(uid);
        UpdateTab(Tab.SelectedPage);
    }

    private void UpdateTab(Control? page)
    {
        if (_uid != null && page is IInfoTab tab)
            tab.UpdateInfo(_uid.Value);
    }
}

public interface IInfoTab
{
    void UpdateInfo(EntityUid uid);
}
