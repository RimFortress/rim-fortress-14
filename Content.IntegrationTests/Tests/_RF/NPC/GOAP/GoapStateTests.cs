using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._RF.NPC.GOAP;

[TestFixture]
public sealed class GoapStateTests : GameTest
{
    [SidedDependency(Side.Server)] private readonly GoapSystem _goap = default!;

    private static readonly StateKey<int> KeyA = "A";
    private static readonly StateKey<bool> KeyB = "B";
    private static readonly StateKey<int> CountKey = "Count";
    private static readonly StateKey<bool> FlagKey = "Flag";

    [Test]
    public void Clone_IsIndependent()
    {
        var state = new GoapState();
        state.SetValue(KeyA, 1);
        state.SetValue(KeyB, true);

        var clone = state.ShallowClone();
        clone.SetValue(KeyA, 5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(state.GetValue(KeyA), Is.EqualTo(1));
            Assert.That(clone.GetValue(KeyA), Is.EqualTo(5));
            Assert.That(state, Has.Count.EqualTo(2));
            Assert.That(clone, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void OverwriteFrom_CopiesValues()
    {
        var left = new GoapState();
        left.SetValue(KeyA, 1);

        var right = new GoapState();
        right.SetValue(KeyA, 2);
        right.SetValue(KeyB, false);

        left.OverwriteFrom(right);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(left.GetValue(KeyA), Is.EqualTo(2));
            Assert.That(left.GetValue(KeyB), Is.False);
        }

    }

    [Test]
    public void GetStateDump_ContainsTypeAndValue()
    {
        var state = new GoapState();
        state.SetValue(CountKey, 3);
        state.SetValue(FlagKey, true);

        var dump = state.GetStateDump();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dump.State[CountKey].Type, Does.Contain("Int32"));
            Assert.That(dump.State[CountKey].Value, Is.EqualTo("3"));
            Assert.That(dump.State[FlagKey].Type, Does.Contain("Boolean"));
            Assert.That(dump.State[FlagKey].Value, Is.EqualTo("True"));
        }
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void GoapStateEcsDefaultsTest()
    {
        var agent = SSpawn(null);
        var comp = SEntMan.EnsureComponent<GoapComponent>(agent);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_goap.TryGetValue(comp.State, GoapState.Owner, out var owner), Is.True);
            Assert.That(owner, Is.EqualTo(agent));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_goap.TryGetValue(comp.State, GoapState.OwnerCoordinates, out var ownerCoords), Is.True);
            Assert.That(ownerCoords, Is.EqualTo(EntityCoordinates.Invalid));
        }
    }
}
