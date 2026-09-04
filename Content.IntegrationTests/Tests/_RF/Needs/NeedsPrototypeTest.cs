using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.Needs.Components;
using Content.Shared._RF.Needs.Prototypes;
using Content.Shared._RF.Needs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RF.Needs;

[TestOf(typeof(NeedsComponent))]
[TestOf(typeof(NeedPrototype))]
[TestOf(typeof(NeedCategoryPrototype))]
[TestOf(typeof(NeedThresholdCategoryPrototype))]
public sealed class NeedsPrototypeTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly IPrototypeManager _proto = default!;
    [SidedDependency(Side.Server)] private readonly IComponentFactory _factory = default!;
    [SidedDependency(Side.Server)] private readonly NeedsSystem _needs = default!;

    [Test]
    [RunOnSide(Side.Server)]
    public void NeedPrototypesValid()
    {
        foreach (var proto in _proto.EnumeratePrototypes<NeedPrototype>())
        {
            if (Pair.IsTestPrototype(proto))
                continue;

            foreach (var threshold in proto.Thresholds)
            {
                Assert.That(proto.Thresholds.Count(x => x.Id == threshold.Id),
                    Is.EqualTo(1),
                    $"duplicated threshold id {threshold.Id} in {proto.ID}");
            }
        }
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void EntityPrototypesValid()
    {
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (Pair.IsTestPrototype(proto))
                continue;

            if (proto.TryComp(out NeedsComponent comp, _factory))
            {
                foreach (var data in comp.Needs)
                {
                    var need = _proto.Index(data.Id);
                    Assert.That(_needs.TryGetNeedsByCategory(need.Category, out var needs), Is.True);
                    Assert.That(needs, Does.Not.Null);
                    Assert.That(comp.Needs.Count(x => needs!.Contains(x.Id)),
                        Is.EqualTo(1),
                        $"entity {proto.ID} contains multiple need from one category {need.Category}");
                }
            }

            if (proto.TryComp(out ModifyNeedOnSleepComponent modifyNeedOnSleep, _factory))
                ValidateCategoryThreshold(modifyNeedOnSleep.Modifiers);

            if (proto.TryComp(out ModifySpeedOnNeedComponent modifySpeedOnNeed, _factory))
                ValidateCategoryThreshold(modifySpeedOnNeed.Modifiers);
        }
    }

    private void ValidateCategoryThreshold<T>(
        Dictionary<ProtoId<NeedCategoryPrototype>, Dictionary<ProtoId<NeedThresholdCategoryPrototype>, T>> dict)
    {
        foreach (var (category, needs) in dict)
        {
            foreach (var (need, _) in needs)
            {
                ValidateCategoryThreshold(category, need);
            }
        }
    }

    private void ValidateCategoryThreshold(
        ProtoId<NeedCategoryPrototype> category,
        ProtoId<NeedThresholdCategoryPrototype> threshold)
    {
        Assert.That(_needs.TryGetNeedsByCategory(category, out var categoryNeeds), Is.True);
        Assert.That(categoryNeeds, Does.Not.Null);

        Assert.That(_needs.TryGetNeedsByThreshold(threshold, out var thresholdNeeds), Is.True);
        Assert.That(thresholdNeeds, Does.Not.Null);

        Assert.That(thresholdNeeds!.Any(x => categoryNeeds!.Contains(x)),
            Is.True,
            $"needs prototype from category {category} with a threshold {threshold} doesn't exist");
    }
}
