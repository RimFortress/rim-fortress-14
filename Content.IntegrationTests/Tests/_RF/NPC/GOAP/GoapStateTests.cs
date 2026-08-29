using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.IntegrationTests.Tests._RF.NPC.GOAP;

[TestOf(typeof(GoapState))]
public sealed class GoapStateTests : GameTest
{
    private static readonly StateKey<int> KeyA = "A";
    private static readonly StateKey<bool> KeyB = "B";
    private static readonly StateKey<int> CountKey = "Count";
    private static readonly StateKey<bool> FlagKey = "Flag";

    private static readonly StateKey<bool> TestBoolKey = "UnitTest/Bool";
    private static readonly StateKey<float> TestFloatKey = "UnitTest/Float";
    private static readonly StateKey<int> TestIntKey = "UnitTest/Int";

    /// <summary>
    /// Setting the same value twice must be a true no-op for the hash.
    /// <see cref="GoapState.SetValue{T}"/> is expected to short-circuit when the
    /// new value equals the old one; if it instead always XORed the entry into
    /// <see cref="GoapState.CachedHash"/> unconditionally, repeated identical
    /// writes (which happen constantly from ECS-default refreshes) would
    /// silently desync the hash from the actual dictionary content.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestSetValueSameValueIsNoOp()
    {
        var state = new GoapState();
        state.SetValue(TestIntKey, 5);
        var hashAfterFirstSet = state.CachedHash;
        var countAfterFirstSet = state.Count;

        state.SetValue(TestIntKey, 5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(state.CachedHash, Is.EqualTo(hashAfterFirstSet));
            Assert.That(state.Count, Is.EqualTo(countAfterFirstSet));
        }
    }

    /// <summary>
    /// Set followed by Remove of the same key must restore the state exactly to
    /// what it was before (hash and count), including from an empty baseline.
    /// The hash toggling relies on XOR being applied symmetrically on write and
    /// removal; any asymmetry produces a "valid-looking" but wrong hash that
    /// only manifests as an incorrect equality/dedup decision much later.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestSetThenRemoveRestoresState()
    {
        var state = new GoapState();
        var emptyHash = state.CachedHash;
        var emptyCount = state.Count;

        state.SetValue(TestFloatKey, 3.14f);
        var removed = state.Remove(TestFloatKey, out var removedValue);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(removed, Is.True);
            Assert.That(removedValue, Is.EqualTo(3.14f));
            Assert.That(state.CachedHash, Is.EqualTo(emptyHash));
            Assert.That(state.Count, Is.EqualTo(emptyCount));
        }
    }

    /// <summary>
    /// Removing a key that was never set must be a pure no-op - in particular it
    /// must not perturb <see cref="GoapState.CachedHash"/> for unrelated existing
    /// keys.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestRemoveMissingKeyIsNoOp()
    {
        var state = new GoapState();
        state.SetValue(TestBoolKey, true);
        var hashBefore = state.CachedHash;

        var removed = state.Remove(TestIntKey, out var value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(removed, Is.False);
            Assert.That(value, Is.EqualTo(0));
            Assert.That(state.CachedHash, Is.EqualTo(hashBefore));
        }
    }

    /// <summary>
    /// Equals/GetHashCode must be independent of insertion order. The planner's
    /// branch-and-bound search de-duplicates <see cref="GoapState"/> snapshots
    /// produced along different action orderings - if two logically identical
    /// states hashed differently depending on the order keys were written in,
    /// the search would silently fail to prune equivalent branches (or treat
    /// equivalent states as distinct) without ever throwing.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestEqualityIsOrderIndependent()
    {
        var a = new GoapState();
        a.SetValue(TestIntKey, 1);
        a.SetValue(TestBoolKey, true);
        a.SetValue(TestFloatKey, 2.5f);

        var b = new GoapState();
        b.SetValue(TestFloatKey, 2.5f);
        b.SetValue(TestBoolKey, true);
        b.SetValue(TestIntKey, 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(a.CachedHash, Is.EqualTo(b.CachedHash));
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }
    }

    /// <summary>
    /// <see cref="GoapState.ShallowClone"/> must produce an equal-but-independent
    /// copy. If the clone shared the backing dictionary, or the two instances'
    /// hash bookkeeping diverged, mutating one would silently bleed into the
    /// other - surfacing only as unrelated, hard-to-reproduce planner glitches.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestShallowCloneIsIndependent()
    {
        var original = new GoapState();
        original.SetValue(TestIntKey, 10);

        var clone = original.ShallowClone();
        clone.SetValue(TestIntKey, 20);

        Assert.Multiple(() =>
        {
            Assert.That(original.GetValue(TestIntKey), Is.EqualTo(10));
            Assert.That(clone.GetValue(TestIntKey), Is.EqualTo(20));
            Assert.That(original, Is.Not.EqualTo(clone));
        });
    }

    /// <summary>
    /// An or-key ("A||B") must fall through to a later part when an earlier one
    /// is absent from the state. This is the exact mechanism conditions rely on
    /// for "either of these keys" checks via
    /// <see cref="SharedGoapSystem.TryGetValueNoEcsDefaults{T}"/> - a broken split
    /// (e.g. an off-by-one in the split flags) would quietly make such
    /// conditions permanently unsatisfiable instead of throwing.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestOrKeyFallsThroughToSecondPart()
    {
        StateKey<bool> orKey = "PartA || PartB";
        var state = new GoapState();
        state.SetValue(new StateKey<bool>("PartB"), true);

        var found = SharedGoapSystem.TryGetValueNoEcsDefaults(state, orKey, out var value);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(value, Is.True);
        });
    }

    /// <summary>
    /// A key containing the domain separator ("/") must always be classified as
    /// an entity default, even if it was never registered via
    /// <c>RegisterEcsDefault</c>. <c>GoapSystem.Graph</c> relies on exactly this
    /// check to decide which effects it can trust when probing dummy-entity
    /// edges for the static dependency graph - if a domain key slipped through
    /// as "not an entity default", the graph builder could bake in a static edge
    /// based on an untrustworthy dummy value, producing a plan that looks fine at
    /// build time but misbehaves at runtime.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestDomainKeyIsAlwaysEntityDefault()
    {
        StateKey<object> unregisteredDomainKey = "Query/SomeUnregisteredQuery";

        Assert.That(GoapState.IsEntityDefault(unregisteredDomainKey), Is.True);
    }

    /// <summary>
    /// A plain, non-domain, unregistered key must NOT be classified as an entity
    /// default - otherwise the static graph builder would wrongly exempt
    /// ordinary planner-tracked keys from requiring a producing edge, letting the
    /// planner silently accept plans that never actually produce a required
    /// precondition.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestPlainKeyIsNotEntityDefault()
    {
        StateKey<object> plainKey = "SomeCustomPlannerKey";

        Assert.That(GoapState.IsEntityDefault(plainKey), Is.False);
    }

    /// <summary>
    /// <see cref="GoapState.Contains"/> must use value equality for boxed value
    /// types. Since the backing dictionary stores values as <c>object</c>, a slip
    /// towards reference equality would make Contains report false for two
    /// structurally-equal-but-separately-boxed floats - this would not throw, it
    /// would simply make conditions never satisfy.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestContainsUsesValueEqualityForBoxedStructs()
    {
        var state = new GoapState();
        state.SetValue(TestFloatKey, 7.0f);

        // Deliberately produced as a separately-boxed float rather than reusing
        // the same boxed instance used for the write above.
        var lookupValue = float.Parse("7.0");

        Assert.That(state.Contains(TestFloatKey, lookupValue), Is.True);
    }

    /// <summary>
    /// <see cref="DomainKey.TryGetParams{TOther,TP1}"/> must reject a parts array
    /// whose length doesn't match the domain's own segment count, rather than
    /// relying on index bounds happening to line up. This guards every
    /// domain-resolution path in <c>SharedGoapSystem</c> (queries, engagements,
    /// datasets) against silently returning garbage for a malformed key instead
    /// of just failing the match.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void TestDomainKeyRejectsWrongPartLength()
    {
        // QueryDomain is declared as "Query/ProtoId" - exactly 2 segments.
        StateKey<object>[] tooFewParts = { "Query" };
        StateKey<object>[] tooManyParts = { "Query", "SomeId", "Extra" };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(GoapState.QueryDomain.TryGetParams<object, string>(tooFewParts, out _), Is.False);
            Assert.That(GoapState.QueryDomain.TryGetParams<object, string>(tooManyParts, out _), Is.False);
        }
    }

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
}
