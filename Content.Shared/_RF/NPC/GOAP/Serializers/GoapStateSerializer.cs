using Robust.Shared.Reflection;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._RF.NPC.GOAP.Serializers;

public sealed class GoapStateSerializer : ITypeReader<GoapState, MappingDataNode>, ITypeCopier<GoapState>
{
    public ValidationNode Validate(ISerializationManager serializationManager, MappingDataNode node,
        IDependencyCollection dependencies, ISerializationContext? context = null)
    {
        var validated = new List<ValidationNode>();

        if (node.Count <= 0)
            return new ValidatedSequenceNode(validated);

        var reflection = dependencies.Resolve<IReflectionManager>();

        foreach (var (key, value) in node)
        {
            if (value.Tag == null)
            {
                validated.Add(new ErrorNode(node.GetKeyNode(key), $"Unable to validate {key}'s type"));
                continue;
            }

            var typeString = value.Tag[6..];

            if (!reflection.TryLooseGetType(typeString, out var type))
            {
                validated.Add(new ErrorNode(node.GetKeyNode(key), $"Unable to find type for {typeString}"));
                continue;
            }

            var validatedNode = serializationManager.ValidateNode(type, value, context);
            validated.Add(validatedNode);
        }

        return new ValidatedSequenceNode(validated);
    }

    public GoapState Read(ISerializationManager serializationManager, MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx, ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<GoapState>? instanceProvider = null)
    {
        var state = instanceProvider != null ? instanceProvider() : new GoapState();

        if (node.Count <= 0)
            return state;

        var reflection = dependencies.Resolve<IReflectionManager>();

        foreach (var (key, value) in node)
        {
            if (value.Tag == null)
                throw new NullReferenceException($"Found null tag for {key}");

            var typeString = value.Tag[6..];

            if (!reflection.TryLooseGetType(typeString, out var type))
                throw new NullReferenceException($"Found null type for {key}");

            var bbData = serializationManager.Read(type, value, hookCtx, context);

            if (bbData == null)
                throw new NullReferenceException($"Found null data for {key}, expected {type}");

            state.SetValue(key, bbData);
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
        target.Clear();
        using var enumerator = source.GetEnumerator();

        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;
            target.SetValue(current.Key, current.Value);
        }
    }
}
