using Content.Client._RF.Stylesheets;
using Content.Shared._RF.Social.Components;
using Content.Shared._RF.Social.Systems;

namespace Content.Client._RF.Info.Controls;

public sealed partial class MoodInfoTab : InfoTab
{
    [Dependency] private IEntityManager _entity = default!;

    public MoodInfoTab()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
    }

    protected override void Update(EntityUid uid)
    {
        var mood = System<SocialSystem>().GetMood(uid);
        var color = mood switch
        {
            < 0 => StyleFortress.LightBad,
            > 0 => StyleFortress.Good,
            _ => StyleFortress.GraySilver,
        };

        Total.Text = Loc.GetString("mood-info-total", ("color", color), ("mood", mood));

        if (_entity.TryGetComponent(uid, out SocialComponent? comp))
            Container.UpdateInfo(comp.MoodEffects);
    }
}
