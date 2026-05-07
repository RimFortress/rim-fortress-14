using System.Globalization;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._RF.NPC.GOAP.Serializers;

[TypeSerializer]
public sealed class GoapStateSerializer : ITypeReader<GoapState, MappingDataNode>, ITypeCopier<GoapState>
{
    public ValidationNode Validate(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        var validated = new List<ValidationNode>(node.Count);

        if (node.Count <= 0)
            return new ValidatedSequenceNode(validated);

        foreach (var (key, value) in node)
        {
            if (TryParseScalar(value, out _))
                return new ValidatedValueNode(node.GetKeyNode(key));

            validated.Add(serializationManager.ValidateNode<object>(value, context));
        }

        return new ValidatedSequenceNode(validated);
    }

    public GoapState Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<GoapState>? instanceProvider = null)
    {
        var state = instanceProvider != null ? instanceProvider() : new GoapState();

        if (node.Count <= 0)
            return state;

        foreach (var (key, value) in node)
        {
            if (TryParseScalar(value, out var result))
            {
                state.SetValue(key, result);
                continue;
            }

            state.SetValue(
                key,
                serializationManager.Read<object>(value, hookCtx, context, notNullableOverride: true));
        }

        return state;
    }

    public void CopyTo(
        ISerializationManager serializationManager,
        GoapState source,
        ref GoapState target,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null)
    {
        target = source.ShallowClone();
    }

    private static bool TryParseScalar(DataNode node, out object value)
    {
        value = default!;

        if (node is not ValueDataNode valueNode)
            return false;

        if (bool.TryParse(valueNode.Value, out var boolValue))
        {
            value = boolValue;
            return true;
        }

        if (int.TryParse(valueNode.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            value = intValue;
            return true;
        }

        if (float.TryParse(valueNode.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
        {
            value = floatValue;
            return true;
        }

        return false;
    }
}
