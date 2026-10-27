#nullable enable
using System.Collections.Generic;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Conditions;
using Content.Shared._RF.NPC.GOAP.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RF.NPC.GOAP;

/// <summary>
/// Checks that GOAP conditions in YAML do not compare state keys with values of the wrong type.
/// </summary>
/// <remarks>
/// Search results (<c>Query/*</c> keys) contain entities (or lists of entities), not bools or numbers.
/// A condition such as <c>Query/Pickaxe == true</c> is parsed successfully and passes prototype validation,
/// but throws "Key contains EntityUid, expected Boolean" in <see cref="GoapState.TryGetValue{T}"/>
/// the first time an NPC plans with it. Use <c>Query/Pickaxe != null</c> instead.
/// </remarks>
[TestFixture]
[TestOf(typeof(GoapCondition))]
public sealed class GoapConditionKeyTypesTest : GameTest
{
    private const string QueryPrefix = "Query" + GoapState.KeyDomainSeparator;

    [SidedDependency(Side.Server)] private readonly IPrototypeManager _proto = default!;

    [Test]
    [RunOnSide(Side.Server)]
    public void QueryKeysAreOnlyCheckedForExistence()
    {
        var errors = new List<string>();

        foreach (var goal in _proto.EnumeratePrototypes<ExecutableGoalPrototype>())
        {
            if (Pair.IsTestPrototype(goal))
                continue;

            CheckConditions(goal.Conditions, $"{nameof(ExecutableGoalPrototype)} {goal.ID}", errors);
        }

        foreach (var goal in _proto.EnumeratePrototypes<UtilityAiGoalPrototype>())
        {
            if (Pair.IsTestPrototype(goal))
                continue;

            CheckConditions(goal.Conditions, $"{nameof(UtilityAiGoalPrototype)} {goal.ID}", errors);
        }

        foreach (var role in _proto.EnumeratePrototypes<EngagementRolePrototype>())
        {
            if (Pair.IsTestPrototype(role))
                continue;

            CheckConditions(role.Conditions, $"{nameof(EngagementRolePrototype)} {role.ID}", errors);

            foreach (var (other, conditions) in role.ConditionsFor)
            {
                CheckConditions(conditions, $"{nameof(EngagementRolePrototype)} {role.ID} (for {other})", errors);
            }
        }

        foreach (var compound in _proto.EnumeratePrototypes<GoapCompoundPrototype>())
        {
            if (Pair.IsTestPrototype(compound))
                continue;

            for (var i = 0; i < compound.Tasks.Count; i++)
            {
                CheckConditions(compound.Tasks[i].Preconditions,
                    $"{nameof(GoapCompoundPrototype)} {compound.ID}, task #{i}",
                    errors);
            }
        }

        Assert.That(errors, Is.Empty, string.Join('\n', errors));
    }

    [Test]
    public void ValueConditionsOnQueryKeysAreDetected()
    {
        // Guards the check itself so that it can't silently stop finding the bug it exists for.
        var errors = new List<string>();

        CheckConditions(
            [
                new EqualsBool { Key = "Query/Pickaxe", Value = true },
                new KeyExist { Key = "Query/Pickaxe" },
                new KeyNotExist { Key = "Query/Pickaxe" },
                new EqualsBool { Key = "InContainer", Value = false },
                new Or { Conditions = [new NotEqualsBool { Key = "Query/A || Query/B", Value = true }] },
            ],
            "test",
            errors);

        // EqualsBool on Query/Pickaxe + both halves of the "Query/A || Query/B" key inside Or.
        Assert.That(errors, Has.Count.EqualTo(3));
    }

    private static void CheckConditions(IEnumerable<GoapCondition> conditions, string where, List<string> errors)
    {
        foreach (var condition in conditions)
        {
            if (condition is Or or)
            {
                CheckConditions(or.Conditions, where, errors);
                continue;
            }

            // KeyExist / KeyNotExist are the only valid checks for entity results.
            if (condition is KeyExist or KeyNotExist || GetValueKey(condition) is not { } key)
                continue;

            // Keys can be written as "A || B" to mean "the first one that exists".
            foreach (var part in key.Split("||", StringSplitOptions.TrimEntries))
            {
                if (part.StartsWith(QueryPrefix, StringComparison.Ordinal))
                {
                    errors.Add($"{where}: {condition.GetType().Name} on key '{part}'. " +
                               $"Search results hold entities, so only '!= null' / '== null' can be used.");
                }
            }
        }
    }

    /// <summary>
    /// Returns the key of a value-comparing condition (<see cref="SimpleGoapCondition{T}"/>), or null for any other.
    /// </summary>
    private static string? GetValueKey(GoapCondition condition)
    {
        for (var type = condition.GetType(); type != null; type = type.BaseType)
        {
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SimpleGoapCondition<>))
                continue;

            var field = type.GetField(nameof(SimpleGoapCondition<int>.Key),
                BindingFlags.Public | BindingFlags.Instance);

            var key = field?.GetValue(condition);
            return key?.GetType().GetProperty(nameof(StateKey<int>.Id))?.GetValue(key) as string;
        }

        return null;
    }
}
