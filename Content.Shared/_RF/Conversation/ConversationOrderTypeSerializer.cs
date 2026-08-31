using Robust.Shared.Reflection;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._RF.Conversation;

[TypeSerializer]
public sealed class ConversationOrderTypeSerializer : ITypeReader<ConversationOrderType, MappingDataNode>
{
    private static Type? GetType(MappingDataNode node)
    {
        if (node.Has("lines"))
            return typeof(ConversationBasicOrderType);

        if (node.Has("custom"))
            return typeof(ConversationCustomOrderType);

        return null;
    }

    public ConversationOrderType Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<ConversationOrderType>? instanceProvider = null)
    {
        if (GetType(node) is { } type)
            return (ConversationOrderType)serializationManager.Read(type, node, hookCtx, context)!;

        var reflection = dependencies.Resolve<IReflectionManager>();

        if (node.Tag == null)
            throw new NullReferenceException("Found null tag for ConversationOrderType");

        var typeString = node.Tag[6..];

        if (!reflection.TryLooseGetType(typeString, out type))
            throw new NullReferenceException($"Unable to find type for {typeString}");

        return (ConversationOrderType)serializationManager.Read(type, node, hookCtx, context)!;
    }

    public ValidationNode Validate(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        if (GetType(node) is { } type)
            return serializationManager.ValidateNode(type, node, context);

        var reflection = dependencies.Resolve<IReflectionManager>();

        if (node.Tag == null)
            return new ErrorNode(node, "Unable to validate ConversationOrderType type");

        var typeString = node.Tag[6..];

        if (!reflection.TryLooseGetType(typeString, out type))
            return new ErrorNode(node, $"Unable to find type for {typeString}");

        return serializationManager.ValidateNode(type, node, context);
    }
}
