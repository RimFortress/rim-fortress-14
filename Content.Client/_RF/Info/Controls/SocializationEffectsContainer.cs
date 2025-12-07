using System.Linq;
using Content.Shared._RF.Socialization;
using Content.Shared._RF.Socialization.Systems;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Info.Controls;

public sealed class SocializationEffectsContainer : BoxContainer
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IEntityManager _entity = default!;

    private SocializationSystem _socialization = default!;

    private bool _setup;

    public SocializationEffectsContainer()
    {
        IoCManager.InjectDependencies(this);
    }

    private void EnsureSetup()
    {
        if (_setup)
            return;

        _setup = true;
        _socialization = _entity.System<SocializationSystem>();
    }

    public void UpdateInfo(Dictionary<ProtoId<SocializationEffectPrototype>, TimeSpan?> effects)
    {
        EnsureSetup();

        var activeEffects = new Dictionary<ProtoId<SocializationEffectPrototype>, (int Value, TimeSpan? EndAt)>();
        foreach (var (protoId, endAt) in effects)
        {
            activeEffects[protoId] = (_socialization.GetEffect(protoId, endAt), endAt);
        }

        RemoveMissingEffects(activeEffects.Keys);

        foreach (var (protoId, (value, endAt)) in activeEffects)
        {
            UpdateOrAddEffect(protoId, value, endAt);
        }

        SortEffectsByValue();
        RefreshStyles();
    }

    private void RemoveMissingEffects(ICollection<ProtoId<SocializationEffectPrototype>> activeProtoIds)
    {
        var toRemove = new List<Control>();

        foreach (var control in Children)
        {
            if (control is SocializationEffectInfo info && !activeProtoIds.Contains(info.Proto))
                toRemove.Add(control);
        }

        foreach (var control in toRemove)
        {
            RemoveChild(control);
        }
    }

    private void UpdateOrAddEffect(ProtoId<SocializationEffectPrototype> protoId, int value, TimeSpan? endAt)
    {
        if (Children.FirstOrDefault(x => x is SocializationEffectInfo info && info.Proto == protoId) is { } existing)
        {
            // Update existing effect
            var info = (SocializationEffectInfo) existing;

            if (info.Value != value || info.EndAt != endAt)
                info.UpdateInfo(_prototype.Index(protoId), value, endAt);
        }
        else
        {
            // Add new effect
            var info = new SocializationEffectInfo();

            info.UpdateInfo(_prototype.Index(protoId), value, endAt);
            AddChild(info);
        }
    }

    /// <summary>
    /// Sort effects by value
    /// </summary>
    private void SortEffectsByValue()
    {
        var sortedChildren = Children
            .Where(c => c is SocializationEffectInfo)
            .Cast<SocializationEffectInfo>()
            .OrderByDescending(mi =>  mi.Value)
            .ToList();

        for (var i = 0; i < sortedChildren.Count; i++)
        {
            var child = sortedChildren[i];

            if (child.GetPositionInParent() != i)
                child.SetPositionInParent(i);
        }
    }

    private void RefreshStyles()
    {
        var even = true;

        foreach (var child in Children)
        {
            if (child is not SocializationEffectInfo { Visible: true } info)
                continue;

            if (even)
            {
                info.AddStyleClass(SocializationEffectInfo.EvenRowStyleClass);
                info.RemoveStyleClass(SocializationEffectInfo.OddRowStyleClass);
            }
            else
            {
                info.AddStyleClass(SocializationEffectInfo.OddRowStyleClass);
                info.RemoveStyleClass(SocializationEffectInfo.EvenRowStyleClass);
            }

            even = !even;
        }
    }
}

