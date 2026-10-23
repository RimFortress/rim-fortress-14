using System.Linq;
using Content.Shared._RF.Social.Components;
using Content.Shared._RF.Social.Systems;

namespace Content.Client._RF.Info.Controls;

public sealed partial class OpinionInfoTab : InfoTab
{
    [Dependency] private IEntityManager _entity = default!;

    private readonly Dictionary<EntityUid, OpinionInfoButton> _buttons = new();
    private EntityUid? _shown;

    public OpinionInfoTab()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
    }

    protected override void Update(EntityUid uid)
    {
        var social = System<SocialSystem>();

        if (_shown != uid)
        {
            _buttons.Clear();
            Container.RemoveAllChildren();
        }

        _shown = uid;

        var updated = false;
        var query = _entity.EntityQueryEnumerator<SocialComponent>();

        while (query.MoveNext(out var targetUid, out _))
        {
            if (targetUid == uid)
                continue;

            var effects = social.GetOpinionEffects(uid, targetUid);

            if (_buttons.TryGetValue(targetUid, out var button))
            {
                if (button.Effects == effects)
                    continue;

                button.UpdateInfo(uid, targetUid, effects);
            }
            else
            {
                button = new OpinionInfoButton();
                button.UpdateInfo(uid, targetUid, effects);
                _buttons[targetUid] = button;

                Container.AddChild(button);
            }

            updated = true;
        }

        if (updated)
            SortButtons();
    }

    private void SortButtons()
    {
        var sorted = Container.Children
            .OfType<OpinionInfoButton>()
            .OrderByDescending(x => x.Value)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].GetPositionInParent() != i)
                sorted[i].SetPositionInParent(i);
        }
    }
}
