using Content.Shared._RF.NPC.Search.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._RF.NPC.GOAP.Serializers;

[TypeSerializer]
public sealed class StateKeySerializer<T> : ITypeSerializer<StateKey<T>, ValueDataNode>, ITypeCopyCreator<StateKey<T>> where T : notnull
{
    public StateKey<T> CreateCopy(ISerializationManager serializationManager, StateKey<T> source, IDependencyCollection dependencies, SerializationHookContext hookCtx, ISerializationContext? context = null)
    {
        return source;
    }

    public StateKey<T> Read(ISerializationManager serializationManager, ValueDataNode node, IDependencyCollection dependencies, SerializationHookContext hookCtx, ISerializationContext? context = null, ISerializationManager.InstantiationDelegate<StateKey<T>>? instanceProvider = null)
    {
        return new StateKey<T>(node.Value);
    }

    public ValidationNode Validate(ISerializationManager serializationManager, ValueDataNode node, IDependencyCollection dependencies, ISerializationContext? context = null)
    {
        var proto = dependencies.Resolve<IPrototypeManager>();
        var parts = GoapState.GetOrParts<object>(node.Value);

        if (parts.Length == 0)
        {
            if (!node.Value.StartsWith(GoapState.QueryKeyPrefix))
                return new ValidatedValueNode(node);

            ProtoId<SearchQueryPrototype> protoId = node.Value[GoapState.QueryKeyPrefix.Length..];

            if (!proto.HasIndex(protoId))
                return new ErrorNode(node, $"invalid SearchQuery ProtoId in default key: {protoId}");

            return new ValidatedValueNode(node);
        }

        foreach (var part in parts)
        {
            if (!part.Id.StartsWith(GoapState.QueryKeyPrefix))
                continue;

            ProtoId<SearchQueryPrototype> protoId = part.Id[GoapState.QueryKeyPrefix.Length..];

            if (!proto.HasIndex(protoId))
                return new ErrorNode(node, $"invalid SearchQuery ProtoId in the key part: {protoId}");
        }

        return new ValidatedValueNode(node);
    }

    public DataNode Write(ISerializationManager serializationManager, StateKey<T> value, IDependencyCollection dependencies, bool alwaysWrite = false, ISerializationContext? context = null)
    {
        return new ValueDataNode(value.Id);
    }
}
