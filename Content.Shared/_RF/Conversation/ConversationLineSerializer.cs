using System.Linq;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._RF.Conversation;

[TypeSerializer]
public sealed class ConversationLineSerializer : ITypeSerializer<ConversationLine, MappingDataNode>
{
    public ValidationNode Validate(ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        var mapping = new Dictionary<ValidationNode, ValidationNode>();
        foreach (var (key, val) in node.Children)
        {
            mapping.Add(serializationManager.ValidateNode<string>(node.GetKeyNode(key), context),
                serializationManager.ValidateNode<string>(val, context));
        }

        return new ValidatedMappingNode(mapping);
    }

    public ConversationLine Read(ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<ConversationLine>? instanceProvider = null)
    {
        var (key, value) = node.Children.First();

        return new ConversationLine
        {
            ActorId = key,
            Message = serializationManager.Read<string>(value, hookCtx, context, notNullableOverride: true),
        };
    }

    public DataNode Write(ISerializationManager serializationManager,
        ConversationLine value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        var node = new MappingDataNode();
        node.Add(value.ActorId, value.Message);
        return node;
    }
}
