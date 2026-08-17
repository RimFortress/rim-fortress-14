using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Conditions;
using Content.Shared._RF.NPC.GOAP.Serializers;
using Content.Tests;
using Robust.Shared.IoC;
using Robust.Shared.Serialization.Manager;

namespace Content.IntegrationTests.Tests._RF.NPC.GOAP;

[TestFixture]
[TestOf(typeof(GoapStateSerializer))]
[TestOf(typeof(GoapConditionSerializer))]
[TestOf(typeof(GoapConditionExpression))]
public sealed class GoapSerializationTest : ContentUnitTest
{
    private ISerializationManager _serializationManager = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _serializationManager = IoCManager.Resolve<ISerializationManager>();
        _serializationManager.Initialize();
    }

    [TestCase("Count == 1", typeof(EqualsInt))]
    [TestCase("Count == 1.0", typeof(EqualsFloat))]
    [TestCase("Count == -1", typeof(EqualsInt))]
    [TestCase("Count == -1.5", typeof(EqualsFloat))]
    [TestCase("Count == Number", typeof(EqualsString))]
    public void TryParse_SelectsExpectedNumericType(string text, Type expectedType)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GoapConditionExpression.TryParse(text, out var condition), Is.True);
            Assert.That(condition, Is.TypeOf(expectedType));
        }
    }

    [Test]
    public void ConditionExpression_RejectsUnsupportedBoolOperator()
    {
        Assert.That(
            () => GoapConditionExpression.TryParse("Flag > true", out _),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void GoapStateSerializer_CopyTo_CopiesAllValues()
    {
        var serializer = new GoapStateSerializer();

        var source = new GoapState();
        var key1 = new StateKey<bool>("HasFood");
        var key2 = new StateKey<int>("Count");
        var key3 = new StateKey<float>("Temperature");

        source.SetValue(key1, true);
        source.SetValue(key2, 4);
        source.SetValue(key3, 1.25f);

        var target = new GoapState();

        serializer.CopyTo(
            _serializationManager,
            source,
            ref target,
            dependencies: null!,
            hookCtx: default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(target.Equals(source), Is.True);
            Assert.That(target.GetValue(key1), Is.True);
            Assert.That(target.GetValue(key2), Is.EqualTo(4));
            Assert.That(target.GetValue(key3), Is.EqualTo(1.25f).Within(0.0001f));
        }
    }
}
