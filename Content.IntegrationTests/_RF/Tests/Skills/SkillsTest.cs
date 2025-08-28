using Content.Shared._RF.Skills;
using Content.Shared._RF.Skills.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RF.Tests.Skills;

[TestOf(typeof(SkillsComponent))]
public sealed class SkillsTest
{
    [Test]
    public async Task ValidatePrototypes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        foreach (var proto in protoMan.EnumeratePrototypes<SkillPrototype>())
        {
            Assert.That(proto.LevelExpMultiplier,
                Is.Not.LessThanOrEqualTo(0),
                $"skill level experience multiplier cannot be less than or equal 0, proto: {proto.ID}");
        }
    }
}
