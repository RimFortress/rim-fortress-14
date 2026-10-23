using System.Linq;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Systems;
using Content.Client._RF.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Info.Controls;

public sealed partial class SocializationEffectsContainer : RfBoxContainer
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public SocializationEffectsContainer()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
    }

    public void UpdateInfo(Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?> effects)
    {
        var activeEffects = new Dictionary<ProtoId<SocialEffectPrototype>, (int Value, TimeSpan? EndAt)>();
        foreach (var (protoId, endAt) in effects)
        {
            activeEffects[protoId] = (System<SocialSystem>().GetEffect(protoId, endAt), endAt);
        }

        RemoveMissingEffects(activeEffects.Keys);

        foreach (var (protoId, (value, endAt)) in activeEffects)
        {
            UpdateOrAddEffect(protoId, value, endAt);
        }

        SortEffectsByValue();
    }

    private void RemoveMissingEffects(ICollection<ProtoId<SocialEffectPrototype>> activeProtoIds)
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

    private void UpdateOrAddEffect(ProtoId<SocialEffectPrototype> protoId, int value, TimeSpan? endAt)
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
}
