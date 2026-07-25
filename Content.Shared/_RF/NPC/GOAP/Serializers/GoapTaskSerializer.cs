using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._RF.NPC.GOAP.Serializers;

[TypeSerializer]
public sealed class GoapTaskSerializer : ITypeReader<GoapTask, MappingDataNode>
{
    private Type? GetType(MappingDataNode node)
    {
        if (node.Has("action"))
            return typeof(GoapActionTask);

        if (node.Has("actions"))
            return typeof(GoapCompoundTask);

        if (node.Has("proto"))
            return typeof(GoapCompoundPrototypeTask);

        return null;
    }

    public GoapTask Read(ISerializationManager serializationManager, MappingDataNode node, IDependencyCollection dependencies, SerializationHookContext hookCtx, ISerializationContext? context = null, ISerializationManager.InstantiationDelegate<GoapTask>? instanceProvider = null)
    {
        var type = GetType(node) ??
                    throw new ArgumentException(
                        "Tried to convert invalid YAML node mapping to GoapTask!");

        return (GoapTask)serializationManager.Read(type, node, hookCtx, context)!;
    }

    public ValidationNode Validate(ISerializationManager serializationManager, MappingDataNode node, IDependencyCollection dependencies, ISerializationContext? context = null)
    {
        var type = GetType(node);

        if (type == null)
            return new ErrorNode(node, "No GoapTask type found.");

        return serializationManager.ValidateNode(type, node, context);
    }
}
