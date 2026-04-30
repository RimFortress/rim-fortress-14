using Content.Shared._RF.NPC.GOAP;

namespace Content.IntegrationTests.Tests._RF.NPC.GOAP;

[TestFixture]
public sealed class GoapStateTests
{
    [Test]
    public void Clone_IsIndependent()
    {
        var state = new GoapState();
        state.SetValue<int>("A", 1);
        state.SetValue<bool>("B", true);

        var clone = state.ShallowClone();
        clone.SetValue<int>("A", 5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(state.GetValue<int>("A"), Is.EqualTo(1));
            Assert.That(clone.GetValue<int>("A"), Is.EqualTo(5));
            Assert.That(state, Has.Count.EqualTo(2));
            Assert.That(clone, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void OverwriteFrom_CopiesValues()
    {
        var left = new GoapState();
        left.SetValue<int>("A", 1);

        var right = new GoapState();
        right.SetValue<int>("A", 2);
        right.SetValue<bool>("B", false);

        left.OverwriteFrom(right);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(left.GetValue<int>("A"), Is.EqualTo(2));
            Assert.That(left.GetValue<bool>("B"), Is.False);
        }

    }

    [Test]
    public void GetStateDump_ContainsTypeAndValue()
    {
        var state = new GoapState();
        state.SetValue<int>("Count", 3);
        state.SetValue<bool>("Flag", true);

        var dump = state.GetStateDump();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dump.State["Count"].Type, Does.Contain("Int32"));
            Assert.That(dump.State["Count"].Value, Is.EqualTo("3"));
            Assert.That(dump.State["Flag"].Type, Does.Contain("Boolean"));
            Assert.That(dump.State["Flag"].Value, Is.EqualTo("True"));
        }

    }
}