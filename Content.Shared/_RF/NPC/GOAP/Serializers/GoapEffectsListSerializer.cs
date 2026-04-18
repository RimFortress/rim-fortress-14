using System.Globalization;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._RF.NPC.GOAP.Serializers;

[TypeSerializer]
public sealed class GoapEffectsListSerializer : ITypeReader<GoapEffectsList, MappingDataNode>
{
    public ValidationNode Validate(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        var validated = new List<ValidationNode>();

        if (node.Count <= 0)
            return new ValidatedSequenceNode(validated);

        foreach (var (key, value) in node.Children)
        {
            if (value.Tag == null)
            {
                validated.Add(new ErrorNode(node.GetKeyNode(key), $"Unable to validate {key}'s type"));
                continue;
            }

            ValidationNode validatedNode = TryParseScalar(value.Tag, out _)
                ? new ValidatedValueNode(node.GetKeyNode(key))
                : new ErrorNode(node.GetKeyNode(key), $"Failed to parse GOAP Effect: {key}");

            validated.Add(validatedNode);
        }

        return new ValidatedSequenceNode(validated);
    }

    public GoapEffectsList Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<GoapEffectsList>? instanceProvider = null)
    {
        var effects = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var (keyNode, valueNode) in node.Children)
        {
            if (valueNode is not ValueDataNode valueValue)
                throw new InvalidOperationException($"Effect '{keyNode}' must be a scalar bool, int, or float.");

            effects[keyNode] = ParseScalar(valueValue.Value);
        }

        return new GoapEffectsList(effects);
    }

    private static bool TryParseScalar(string raw, out object value)
    {
        if (bool.TryParse(raw, out var boolValue))
        {
            value = boolValue;
            return true;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            value = intValue;
            return true;
        }

        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
        {
            value = floatValue;
            return true;
        }

        value = default!;
        return false;
    }

    private static object ParseScalar(string raw)
        => TryParseScalar(raw, out var value)
            ? value
            : throw new FormatException($"Invalid GOAP effect value: '{raw}'.");
}
