using System.Globalization;
using System.Text.RegularExpressions;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using Robust.Shared.Serialization.Manager;
using Content.Shared._RF.NPC.GOAP.Conditions;

namespace Content.Shared._RF.NPC.GOAP.Serializers;

/// <summary>
/// Reads GOAP conditions from either:
/// 1) shorthand value nodes like "Key >= 3"
/// 2) normal polymorphic !type mappings.
/// </summary>
[TypeSerializer]
public sealed class GoapConditionSerializer : ITypeReader<GoapCondition, ValueDataNode>
{
    public ValidationNode Validate(
        ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        if (GoapConditionExpression.TryParse(node.Value, out _))
            return new ValidatedValueNode(node);

        // Let the normal Robust polymorphic path validate !type YAML.
        return serializationManager.ValidateNode<object>(node, context);
    }

    public GoapCondition Read(
        ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<GoapCondition>? instanceProvider = null)
    {
        if (GoapConditionExpression.TryParse(node.Value, out var condition))
            return condition;

        // Normal YAML form:
        // - !type:EqualsInt
        //   key: IngredientCount
        //   value: 3
        return serializationManager.Read(node, context, instanceProvider: instanceProvider, notNullableOverride: true);
    }
}

public static class GoapConditionExpression
{
    private static readonly Regex Expr = new(
        @"^\s*(?<key>[A-Za-z_][A-Za-z0-9_./| ]*)\s*(?<op>==|!=|>=|<=|>|<)\s*(?<value>.+?)\s*$",
        RegexOptions.Compiled);

    public static bool TryParse(string text, out GoapCondition condition)
    {
        var match = Expr.Match(text);
        if (!match.Success)
        {
            condition = default!;
            return false;
        }

        var key = match.Groups["key"].Value.Trim();
        var op = match.Groups["op"].Value.Trim();
        var rawValue = match.Groups["value"].Value.Trim();

        if (rawValue.ToLowerInvariant() == "null")
        {
            condition = op switch
            {
                "==" => new KeyNotExist { Key = key },
                "!=" => new KeyExist { Key = key },
                _ => throw new ArgumentException($"Operator '{op}' is not valid for null condition.")
            };
            return true;
        }

        if (bool.TryParse(rawValue, out var boolValue))
        {
            condition = op switch
            {
                "==" => new EqualsBool { Key = key, Value = boolValue },
                "!=" => new NotEqualsBool { Key = key, Value = boolValue },
                _ => throw new ArgumentException($"Operator '{op}' is not valid for bool condition.")
            };
            return true;
        }

        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            condition = op switch
            {
                "==" => new EqualsInt { Key = key, Value = intValue },
                "!=" => new NotEqualsInt { Key = key, Value = intValue },
                ">" => new MoreThanInt { Key = key, Value = intValue },
                ">=" => new MoreThanOrEqualInt { Key = key, Value = intValue },
                "<" => new LessThanInt { Key = key, Value = intValue },
                "<=" => new LessThanOrEqualInt { Key = key, Value = intValue },
                _ => throw new ArgumentException($"Unsupported operator '{op}' for int condition.")
            };
            return true;
        }

        if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
        {
            condition = op switch
            {
                "==" => new EqualsFloat { Key = key, Value = floatValue },
                "!=" => new NotEqualsFloat { Key = key, Value = floatValue },
                ">" => new MoreThanFloat { Key = key, Value = floatValue },
                ">=" => new MoreThanOrEqualFloat { Key = key, Value = floatValue },
                "<" => new LessThanFloat { Key = key, Value = floatValue },
                "<=" => new LessThanOrEqualFloat { Key = key, Value = floatValue },
                _ => throw new ArgumentException($"Unsupported operator '{op}' for float condition.")
            };
            return true;
        }

        condition = op switch
        {
            "==" => new EqualsString { Key = key, Value = rawValue },
            "!=" => new NotEqualsString { Key = key, Value = rawValue },
            _ => throw new ArgumentException($"Unsupported operator '{op}' for string condition."),
        };

        return true;
    }
}
